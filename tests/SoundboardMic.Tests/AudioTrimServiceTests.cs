using NAudio.Utils;
using NAudio.Wave;
using SoundboardMic.App.Services;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Models;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

public class AudioTrimServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AudioRepository _audios;
    private readonly MapeamentoRepository _mapeamentos;
    private readonly CategoriaRepository _categorias;
    private readonly AudioFileCache _cache;
    private readonly string _dir;
    private readonly AudioTrimService _service;

    public AudioTrimServiceTests()
    {
        _audios = new AudioRepository(_db.Factory);
        _mapeamentos = new MapeamentoRepository(_db.Factory);
        _categorias = new CategoriaRepository(_db.Factory);
        _dir = Path.Combine(Path.GetTempPath(), $"soundboardmic-trimsvc-{Guid.NewGuid():N}");
        _cache = new AudioFileCache(_dir);
        _service = new AudioTrimService(_audios, _cache);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static byte[] Wav(double segundos, int taxa = 8000)
    {
        using var ms = new MemoryStream();
        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(ms), new WaveFormat(taxa, 16, 1)))
        {
            for (var i = 0; i < (int)(segundos * taxa); i++)
                writer.WriteSample(0.25f);
        }
        return ms.ToArray();
    }

    private async Task<Audio> AdicionarAsync(string nome, long? categoriaId, int ordem, double segundos = 3)
    {
        var (caminho, conteudo, nomeArquivo) = await _cache.ImportarBytesAsync(Wav(segundos), $"{nome}.wav");
        return await _audios.AddAsync(new Audio
        {
            Nome = nome,
            CaminhoArquivo = caminho,
            ArquivoConteudo = conteudo,
            ArquivoNomeOriginal = nomeArquivo,
            DuracaoMs = (long)(segundos * 1000),
            VolumePadrao = 0.7,
            Icone = "E768",
            Cor = "#FF8800",
            CategoriaId = categoriaId,
            Ordem = ordem,
        });
    }

    [Fact]
    public async Task CriarNovo_HerdaCamposFicaLogoAposOOriginalESemAtalho()
    {
        var cat = await _categorias.AddAsync(new Categoria { Nome = "Memes", Ordem = 0 });
        var a = await AdicionarAsync("A", cat.Id, 0);
        var original = await AdicionarAsync("Buzina", cat.Id, 1);
        var c = await AdicionarAsync("C", cat.Id, 2);
        var outraCategoria = await AdicionarAsync("X", null, 2);
        await _mapeamentos.AddAsync(new Mapeamento { AudioId = original.Id, Teclas = "Ctrl+1" });

        var wav = AudioTrimmer.CortarParaWav(original.CaminhoArquivo, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        var novo = await _service.CriarNovoAsync(original, wav, "  ");

        var lido = (await _audios.GetByIdAsync(novo.Id))!;
        Assert.Equal("Buzina (corte)", lido.Nome);
        Assert.Equal(cat.Id, lido.CategoriaId);
        Assert.Equal(0.7, lido.VolumePadrao, 6);
        Assert.Equal("E768", lido.Icone);
        Assert.Equal("#FF8800", lido.Cor);
        Assert.Equal(2, lido.Ordem);
        Assert.InRange(lido.DuracaoMs, 995, 1005);
        Assert.EndsWith(".wav", lido.ArquivoNomeOriginal);
        Assert.True(File.Exists(lido.CaminhoArquivo));
        Assert.Equal(wav, lido.ArquivoConteudo);
        Assert.Empty(await _mapeamentos.GetByAudioIdAsync(novo.Id));

        Assert.Equal(0, (await _audios.GetByIdAsync(a.Id))!.Ordem);
        Assert.Equal(1, (await _audios.GetByIdAsync(original.Id))!.Ordem);
        Assert.Equal(3, (await _audios.GetByIdAsync(c.Id))!.Ordem);
        Assert.Equal(2, (await _audios.GetByIdAsync(outraCategoria.Id))!.Ordem);
    }

    [Fact]
    public async Task CriarNovo_UsaNomeInformado()
    {
        var original = await AdicionarAsync("Grito", null, 0);
        var wav = AudioTrimmer.CortarParaWav(original.CaminhoArquivo, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        var novo = await _service.CriarNovoAsync(original, wav, "Grito curto");

        Assert.Equal("Grito curto", (await _audios.GetByIdAsync(novo.Id))!.Nome);
    }

    [Fact]
    public async Task Substituir_MantemIdentidadeEAtalhoETrocaConteudo()
    {
        var cat = await _categorias.AddAsync(new Categoria { Nome = "Memes", Ordem = 0 });
        var original = await AdicionarAsync("Buzina", cat.Id, 4);
        var mapa = await _mapeamentos.AddAsync(new Mapeamento { AudioId = original.Id, Teclas = "Ctrl+1" });
        var cacheAntigo = original.CaminhoArquivo;

        var wav = AudioTrimmer.CortarParaWav(original.CaminhoArquivo, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1250));
        await _service.SubstituirAsync(original, wav);

        var lido = (await _audios.GetByIdAsync(original.Id))!;
        Assert.Equal("Buzina", lido.Nome);
        Assert.Equal(cat.Id, lido.CategoriaId);
        Assert.Equal(4, lido.Ordem);
        Assert.Equal(0.7, lido.VolumePadrao, 6);
        Assert.InRange(lido.DuracaoMs, 745, 755);
        Assert.Equal(wav, lido.ArquivoConteudo);
        Assert.EndsWith(".wav", lido.ArquivoNomeOriginal);
        Assert.NotEqual(cacheAntigo, lido.CaminhoArquivo);
        Assert.True(File.Exists(lido.CaminhoArquivo));
        Assert.False(File.Exists(cacheAntigo));
        Assert.Equal("Ctrl+1", (await _mapeamentos.GetByIdAsync(mapa.Id))!.Teclas);
    }

    [Fact]
    public async Task Substituir_FalhaNoBanco_PreservaOriginalEApagaCacheNovo()
    {
        var original = await AdicionarAsync("Buzina", null, 0);
        var cacheAntigo = original.CaminhoArquivo;
        var conteudoAntigo = original.ArquivoConteudo;
        await _audios.DeleteAsync(original.Id); // UpdateAsync vai retornar false

        var wav = AudioTrimmer.CortarParaWav(cacheAntigo, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubstituirAsync(original, wav));

        Assert.Equal(cacheAntigo, original.CaminhoArquivo);
        Assert.Same(conteudoAntigo, original.ArquivoConteudo);
        Assert.True(File.Exists(cacheAntigo));
        Assert.Single(Directory.GetFiles(_dir));
    }
}
