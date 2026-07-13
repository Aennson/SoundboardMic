using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

public class MapeamentoRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AudioRepository _audios;
    private readonly MapeamentoRepository _repo;
    private readonly Audio _audio;

    public MapeamentoRepositoryTests()
    {
        _audios = new AudioRepository(_db.Factory);
        _repo = new MapeamentoRepository(_db.Factory);
        _audio = _audios.AddAsync(new Audio
        {
            Nome = "Tada",
            CaminhoArquivo = @"C:\sons\tada.wav",
        }).GetAwaiter().GetResult();
    }

    public void Dispose() => _db.Dispose();

    private Mapeamento NovoMapeamento(string teclas = "Ctrl+Alt+F1") => new()
    {
        AudioId = _audio.Id,
        Teclas = teclas,
    };

    [Fact]
    public async Task Add_AtribuiIdEPersiste()
    {
        var map = await _repo.AddAsync(NovoMapeamento());

        Assert.True(map.Id > 0);

        var lido = await _repo.GetByIdAsync(map.Id);
        Assert.NotNull(lido);
        Assert.Equal(_audio.Id, lido.AudioId);
        Assert.Equal("Ctrl+Alt+F1", lido.Teclas);
        Assert.True(lido.Ativo);
    }

    [Fact]
    public async Task Add_TeclasDuplicadas_LancaExcecaoDedicada()
    {
        await _repo.AddAsync(NovoMapeamento("Ctrl+Shift+3"));

        var ex = await Assert.ThrowsAsync<TeclasDuplicadasException>(
            () => _repo.AddAsync(NovoMapeamento("Ctrl+Shift+3")));

        Assert.Equal("Ctrl+Shift+3", ex.Teclas);
    }

    [Fact]
    public async Task Add_TeclasVazias_Lanca()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _repo.AddAsync(NovoMapeamento("")));
    }

    [Fact]
    public async Task Add_AudioInexistente_LancaPorViolacaoDeFk()
    {
        var map = NovoMapeamento();
        map.AudioId = 9999;

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => _repo.AddAsync(map));
    }

    [Fact]
    public async Task GetByTeclas_EncontraMapeamento()
    {
        await _repo.AddAsync(NovoMapeamento("Alt+F4"));

        var lido = await _repo.GetByTeclasAsync("Alt+F4");

        Assert.NotNull(lido);
        Assert.Equal(_audio.Id, lido.AudioId);
    }

    [Fact]
    public async Task GetByTeclas_Inexistente_RetornaNull()
    {
        Assert.Null(await _repo.GetByTeclasAsync("Ctrl+Z"));
    }

    [Fact]
    public async Task GetByAudioId_FiltraCorretamente()
    {
        var outroAudio = await _audios.AddAsync(new Audio
        {
            Nome = "Outro",
            CaminhoArquivo = @"C:\sons\outro.wav",
        });

        await _repo.AddAsync(NovoMapeamento("Ctrl+1"));
        await _repo.AddAsync(NovoMapeamento("Ctrl+2"));
        await _repo.AddAsync(new Mapeamento { AudioId = outroAudio.Id, Teclas = "Ctrl+3" });

        var doPrimeiro = await _repo.GetByAudioIdAsync(_audio.Id);

        Assert.Equal(2, doPrimeiro.Count);
        Assert.All(doPrimeiro, m => Assert.Equal(_audio.Id, m.AudioId));
    }

    [Fact]
    public async Task Update_AlteraTeclasEAtivo()
    {
        var map = await _repo.AddAsync(NovoMapeamento());
        map.Teclas = "Ctrl+Alt+F2";
        map.Ativo = false;

        Assert.True(await _repo.UpdateAsync(map));

        var lido = await _repo.GetByIdAsync(map.Id);
        Assert.Equal("Ctrl+Alt+F2", lido!.Teclas);
        Assert.False(lido.Ativo);
    }

    [Fact]
    public async Task Update_ParaTeclasJaUsadas_LancaExcecaoDedicada()
    {
        await _repo.AddAsync(NovoMapeamento("Ctrl+A"));
        var segundo = await _repo.AddAsync(NovoMapeamento("Ctrl+B"));

        segundo.Teclas = "Ctrl+A";

        await Assert.ThrowsAsync<TeclasDuplicadasException>(() => _repo.UpdateAsync(segundo));
    }

    [Fact]
    public async Task Delete_RemoveRegistro()
    {
        var map = await _repo.AddAsync(NovoMapeamento());

        Assert.True(await _repo.DeleteAsync(map.Id));
        Assert.Null(await _repo.GetByIdAsync(map.Id));
    }

    [Fact]
    public async Task DeleteDoAudio_CascateiaParaMapeamentos()
    {
        var map = await _repo.AddAsync(NovoMapeamento());

        await _audios.DeleteAsync(_audio.Id);

        Assert.Null(await _repo.GetByIdAsync(map.Id));
    }
}
