using SoundboardMic.App.Services;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Models;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

public class MyInstantsViewModelTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AudioRepository _audioRepo;
    private readonly AudioFileCache _fileCache = new();
    private readonly FakePlaybackService _player = new();
    private readonly List<string> _arquivosGerados = new();

    public MyInstantsViewModelTests() => _audioRepo = new AudioRepository(_db.Factory);

    public void Dispose()
    {
        _db.Dispose();
        foreach (var caminho in _arquivosGerados)
        {
            try { if (File.Exists(caminho)) File.Delete(caminho); } catch { /* best-effort */ }
        }
    }

    private static readonly byte[] MiniMp3 = { 0x49, 0x44, 0x33, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    private MyInstantsSound NovoSom(string nome = "Buzina Engraçada", string? cor = "#FF0000") => new(
        Nome: nome,
        UrlAudio: $"https://www.myinstants.com/media/sounds/{Guid.NewGuid():N}.mp3",
        UrlPagina: "https://www.myinstants.com/pt/instant/buzina/",
        CorHex: cor);

    private MyInstantsViewModel CriarViewModel(FakeMyInstantsService? service = null) =>
        new(service ?? new FakeMyInstantsService(new[] { NovoSom() }), _audioRepo, _fileCache, _player);

    [Fact]
    public async Task CarregarInicialAsync_PopulaListaESoDefinePrimeiraVez()
    {
        var fake = new FakeMyInstantsService(new[] { NovoSom("A"), NovoSom("B") });
        var vm = CriarViewModel(fake);

        await vm.CarregarInicialAsync();
        Assert.Equal(2, vm.Sons.Count);
        Assert.Equal(1, fake.ChamadasBuscar);

        // Segunda chamada não deve rebuscar (já carregado uma vez).
        await vm.CarregarInicialAsync();
        Assert.Equal(1, fake.ChamadasBuscar);
    }

    [Fact]
    public async Task Buscar_ComTermo_PassaTermoAoServicoEAtualizaLista()
    {
        var fake = new FakeMyInstantsService(new[] { NovoSom("Resultado") });
        var vm = CriarViewModel(fake);
        vm.Termo = "buzina";

        await vm.BuscarCommand.ExecuteAsync(null);

        Assert.Equal("buzina", fake.UltimoTermo);
        Assert.Single(vm.Sons);
        Assert.Equal("Resultado", vm.Sons[0].Nome);
    }

    [Fact]
    public async Task Buscar_QuandoServicoFalha_DefineErroEListaVazia()
    {
        var vm = CriarViewModel(new FakeMyInstantsService(falhar: true));

        await vm.BuscarCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Erro);
        Assert.True(vm.ListaVazia);
    }

    [Fact]
    public async Task Tocar_BaixaOArquivoETocaNoPlayer()
    {
        var fake = new FakeMyInstantsService(new[] { NovoSom() }, conteudo: MiniMp3);
        var vm = CriarViewModel(fake);
        await vm.CarregarInicialAsync();
        var item = vm.Sons[0];

        await vm.TocarCommand.ExecuteAsync(item);

        Assert.True(item.Tocando);
        Assert.NotNull(_player.UltimoCaminho);
        Assert.True(File.Exists(_player.UltimoCaminho));
        _arquivosGerados.Add(_player.UltimoCaminho!);
    }

    [Fact]
    public async Task Tocar_DeNovoNoMesmoItem_Para()
    {
        var fake = new FakeMyInstantsService(new[] { NovoSom() }, conteudo: MiniMp3);
        var vm = CriarViewModel(fake);
        await vm.CarregarInicialAsync();
        var item = vm.Sons[0];

        await vm.TocarCommand.ExecuteAsync(item);
        _arquivosGerados.Add(_player.UltimoCaminho!);
        Assert.True(item.Tocando);

        await vm.TocarCommand.ExecuteAsync(item);

        Assert.False(item.Tocando);
        Assert.True(_player.Parado);
    }

    [Fact]
    public async Task Adicionar_ComSucesso_CriaAudioNoRepositorioEMarcaAdicionado()
    {
        var fake = new FakeMyInstantsService(new[] { NovoSom("Meme Legal", "#ABC") }, conteudo: MiniMp3);
        var vm = CriarViewModel(fake);
        await vm.CarregarInicialAsync();
        var item = vm.Sons[0];

        await vm.AdicionarCommand.ExecuteAsync(item);

        Assert.True(item.Adicionado);
        var todos = await _audioRepo.GetAllAsync();
        var salvo = Assert.Single(todos);
        Assert.Equal("Meme Legal", salvo.Nome);
        Assert.Equal("#AABBCC", salvo.Cor); // "#ABC" curto normalizado
        Assert.NotNull(salvo.ArquivoConteudo);
        Assert.True(File.Exists(salvo.CaminhoArquivo));
        _arquivosGerados.Add(salvo.CaminhoArquivo);
    }

    [Fact]
    public async Task Adicionar_QuandoServicoFalha_DefineErroENaoMarcaAdicionado()
    {
        var vm = CriarViewModel(new FakeMyInstantsService(falhar: true));
        var item = new MyInstantsSoundViewModel(NovoSom());

        await vm.AdicionarCommand.ExecuteAsync(item);

        Assert.False(item.Adicionado);
        Assert.NotNull(vm.Erro);
        Assert.Empty(await _audioRepo.GetAllAsync());
    }

    private sealed class FakeMyInstantsService : IMyInstantsService
    {
        private readonly IReadOnlyList<MyInstantsSound> _resultado;
        private readonly byte[] _conteudo;
        private readonly bool _falhar;

        public int ChamadasBuscar { get; private set; }
        public string? UltimoTermo { get; private set; }

        public FakeMyInstantsService(IReadOnlyList<MyInstantsSound>? resultado = null, byte[]? conteudo = null, bool falhar = false)
        {
            _resultado = resultado ?? Array.Empty<MyInstantsSound>();
            _conteudo = conteudo ?? Array.Empty<byte>();
            _falhar = falhar;
        }

        public Task<IReadOnlyList<MyInstantsSound>> BuscarAsync(string? termo, CancellationToken ct = default)
        {
            ChamadasBuscar++;
            UltimoTermo = termo;
            if (_falhar) throw new InvalidOperationException("Falha simulada de rede.");
            return Task.FromResult(_resultado);
        }

        public Task<byte[]> BaixarAudioAsync(MyInstantsSound som, CancellationToken ct = default)
        {
            if (_falhar) throw new InvalidOperationException("Falha simulada de download.");
            return Task.FromResult(_conteudo);
        }
    }

    private sealed class FakePlaybackService : IPlaybackService
    {
        public bool IsPlaying { get; private set; }
        public string? UltimoCaminho { get; private set; }
        public bool Parado { get; private set; }

        public event EventHandler? PlaybackStopped;

        public void Play(string filePath, string? deviceId = null, float volume = 1.0f)
        {
            UltimoCaminho = filePath;
            IsPlaying = true;
            Parado = false;
        }

        public void Stop()
        {
            IsPlaying = false;
            Parado = true;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() { }
    }
}
