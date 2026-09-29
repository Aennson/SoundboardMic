using System.Collections.ObjectModel;
using NAudio.Utils;
using NAudio.Wave;
using SoundboardMic.App.Services;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.Hotkeys;
using SoundboardMic.Core.Models;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

[Collection("WPF")]
public class EditorDeSonsViewModelTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AudioRepository _audios;
    private readonly AudioFileCache _cache;
    private readonly string _dir;
    private readonly MyInstantsViewModelTests.FakePlaybackService _player = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly SoundboardController _controller;
    private readonly SoundboardControllerTests.FakeSettings _settings = new();

    public EditorDeSonsViewModelTests()
    {
        _audios = new AudioRepository(_db.Factory);
        _dir = Path.Combine(Path.GetTempPath(), $"soundboardmic-editorvm-{Guid.NewGuid():N}");
        _cache = new AudioFileCache(_dir);
        var hook = new SoundboardControllerTests.FakeHook();
        _controller = new SoundboardController(
            new SoundboardControllerTests.FakeEngine(), hook, new HotkeyDispatcher(hook),
            new SoundboardControllerTests.FakeDevices(), _audios, new MapeamentoRepository(_db.Factory), _settings);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private EditorDeSonsViewModel NovoEditor() =>
        new(_player, new AudioTrimService(_audios, _cache), _controller, _settings, _dialogs);

    private static AudioItemViewModel Item(string nome, string caminho = "x.wav") =>
        new(new Audio { Nome = nome, CaminhoArquivo = caminho }, null);

    private async Task<AudioItemViewModel> ItemGravadoAsync(string nome, double segundos = 2)
    {
        using var ms = new MemoryStream();
        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(ms), new WaveFormat(8000, 16, 1)))
        {
            for (var i = 0; i < (int)(segundos * 8000); i++)
                writer.WriteSample(i % 80 < 40 ? 0.5f : -0.5f);
        }
        var (caminho, conteudo, nomeArquivo) = await _cache.ImportarBytesAsync(ms.ToArray(), $"{nome}.wav");
        var audio = await _audios.AddAsync(new Audio
        {
            Nome = nome, CaminhoArquivo = caminho, ArquivoConteudo = conteudo,
            ArquivoNomeOriginal = nomeArquivo, DuracaoMs = (long)(segundos * 1000), VolumePadrao = 1,
        });
        return new AudioItemViewModel(audio, null);
    }

    private static async Task AguardarOndaAsync(EditorDeSonsViewModel vm)
    {
        for (var i = 0; i < 200 && (vm.CarregandoOnda || vm.Duracao == TimeSpan.Zero); i++)
            await Task.Delay(20);
        Assert.False(vm.CarregandoOnda);
        Assert.NotEqual(TimeSpan.Zero, vm.Duracao);
    }

    [Fact]
    public void Busca_IgnoraMaiusculasEAcentos() => WpfTestHost.Run(() =>
    {
        using var vm = NovoEditor();
        var audios = new ObservableCollection<AudioItemViewModel>
        {
            Item("Canção da vitória"), Item("Buzina"), Item("CANCAO triste"),
        };
        vm.Vincular(audios);

        vm.Busca = "cancao";
        var visiveis = vm.AudiosView!.Cast<AudioItemViewModel>().Select(a => a.Nome).ToList();

        Assert.Equal(new[] { "CANCAO triste", "Canção da vitória" }, visiveis.OrderBy(n => n, StringComparer.Ordinal));
        Assert.False(vm.SemAudios);
    });

    [Fact]
    public void Lista_OrdenadaPorNomeESinalizaVazia() => WpfTestHost.Run(() =>
    {
        using var vm = NovoEditor();
        var audios = new ObservableCollection<AudioItemViewModel>();
        vm.Vincular(audios);
        Assert.True(vm.SemAudios);

        audios.Add(Item("Zebra"));
        audios.Add(Item("Abelha"));

        Assert.False(vm.SemAudios);
        Assert.Equal(new[] { "Abelha", "Zebra" }, vm.AudiosView!.Cast<AudioItemViewModel>().Select(a => a.Nome));
    });

    [Fact]
    public void Abrir_LimpaBuscaQueEscondeOAudio() => WpfTestHost.Run(() =>
    {
        using var vm = NovoEditor();
        var alvo = Item("Buzina", Path.Combine(_dir, "nao-existe.wav"));
        vm.Vincular(new ObservableCollection<AudioItemViewModel> { alvo, Item("Grito") });
        vm.Busca = "grito";

        vm.Abrir(alvo);

        Assert.Equal(string.Empty, vm.Busca);
        Assert.Same(alvo, vm.Selecionado);
        Assert.Equal("Buzina (corte)", vm.NomeNovo);
    });

    [Fact]
    public Task Selecao_NormalizaEHabilitaSalvarSoComTrechoParcial() => WpfTestHost.RunAsync(async () =>
    {
        using var vm = NovoEditor();
        var item = await ItemGravadoAsync("Buzina");
        vm.Vincular(new ObservableCollection<AudioItemViewModel> { item });

        vm.Abrir(item);
        await AguardarOndaAsync(vm);

        Assert.NotNull(vm.Picos);
        Assert.Equal(TimeSpan.Zero, vm.Inicio);
        Assert.Equal(vm.Duracao, vm.Fim);
        Assert.False(vm.CriarNovoCommand.CanExecute(null)); // o áudio todo: nada a cortar
        Assert.True(vm.OuvirSelecaoCommand.CanExecute(null));

        vm.InicioTexto = "0:00.500";
        Assert.Equal(TimeSpan.FromMilliseconds(500), vm.Inicio);
        Assert.Equal("0:00.500", vm.InicioTexto);
        Assert.True(vm.CriarNovoCommand.CanExecute(null));
        Assert.True(vm.SubstituirCommand.CanExecute(null));

        vm.InicioTexto = "abc";
        Assert.Equal(TimeSpan.FromMilliseconds(500), vm.Inicio);
        Assert.Equal("0:00.500", vm.InicioTexto);

        // Início além do fim empurra/limita para manter o trecho mínimo.
        vm.Inicio = vm.Duracao;
        Assert.True(vm.Fim - vm.Inicio >= TimeSpan.FromMilliseconds(100));
        Assert.True(vm.Fim <= vm.Duracao);

        vm.SelecionarTudoCommand.Execute(null);
        Assert.Equal(TimeSpan.Zero, vm.Inicio);
        Assert.Equal(vm.Duracao, vm.Fim);
    });

    [Fact]
    public Task OuvirSelecao_TocaOTrechoEAlternaParaParar() => WpfTestHost.RunAsync(async () =>
    {
        using var vm = NovoEditor();
        var item = await ItemGravadoAsync("Buzina");
        vm.Vincular(new ObservableCollection<AudioItemViewModel> { item });
        vm.Abrir(item);
        await AguardarOndaAsync(vm);

        vm.OuvirSelecaoCommand.Execute(null);
        Assert.True(vm.Ouvindo);
        Assert.Equal(item.CaminhoArquivo, _player.UltimoCaminho);
        Assert.Equal("Parar", vm.OuvirTexto);

        vm.OuvirSelecaoCommand.Execute(null);
        Assert.False(vm.Ouvindo);
        Assert.True(_player.Parado);
    });

    [Fact]
    public Task CriarNovo_GravaTrechoEAvisaAJanela() => WpfTestHost.RunAsync(async () =>
    {
        using var vm = NovoEditor();
        var item = await ItemGravadoAsync("Buzina");
        vm.Vincular(new ObservableCollection<AudioItemViewModel> { item });
        long? alterado = null;
        vm.AposAlteracao = id => { alterado = id; return Task.CompletedTask; };
        vm.Abrir(item);
        await AguardarOndaAsync(vm);

        vm.InicioTexto = "00:00.500";
        vm.FimTexto = "00:01.500";
        vm.NomeNovo = "Buzina curta";
        await vm.CriarNovoCommand.ExecuteAsync(null);

        Assert.NotNull(alterado);
        var novo = (await _audios.GetByIdAsync(alterado!.Value))!;
        Assert.Equal("Buzina curta", novo.Nome);
        Assert.InRange(novo.DuracaoMs, 995, 1005);
        Assert.Contains("Buzina curta", vm.Mensagem);
        Assert.False(vm.Ocupado);
    });

    [Fact]
    public Task Substituir_SemConfirmacao_NaoAlteraNada() => WpfTestHost.RunAsync(async () =>
    {
        using var vm = NovoEditor();
        var item = await ItemGravadoAsync("Buzina");
        var caminhoOriginal = item.CaminhoArquivo;
        vm.Vincular(new ObservableCollection<AudioItemViewModel> { item });
        var chamou = false;
        vm.AposAlteracao = _ => { chamou = true; return Task.CompletedTask; };
        vm.Abrir(item);
        await AguardarOndaAsync(vm);
        vm.InicioTexto = "00:01.000";

        _dialogs.Resposta = false;
        await vm.SubstituirCommand.ExecuteAsync(null);

        Assert.True(_dialogs.Perguntou);
        Assert.False(chamou);
        Assert.Equal(2000, (await _audios.GetByIdAsync(item.Id))!.DuracaoMs);
        Assert.True(File.Exists(caminhoOriginal));
    });

    [Fact]
    public Task Substituir_Confirmado_TrocaOAudioERecarregaAOnda() => WpfTestHost.RunAsync(async () =>
    {
        using var vm = NovoEditor();
        var item = await ItemGravadoAsync("Buzina");
        vm.Vincular(new ObservableCollection<AudioItemViewModel> { item });
        long? alterado = null;
        vm.AposAlteracao = id => { alterado = id; return Task.CompletedTask; };
        vm.Abrir(item);
        await AguardarOndaAsync(vm);
        vm.InicioTexto = "00:01.000";

        _dialogs.Resposta = true;
        await vm.SubstituirCommand.ExecuteAsync(null);

        Assert.Equal(item.Id, alterado);
        Assert.InRange((await _audios.GetByIdAsync(item.Id))!.DuracaoMs, 995, 1005);
        Assert.InRange(vm.Duracao.TotalMilliseconds, 995, 1005);
        Assert.Equal("Áudio substituído.", vm.Mensagem);
    });

    private sealed class FakeDialogs : IDialogService
    {
        public bool Resposta { get; set; }
        public bool Perguntou { get; private set; }
        public string? PickAudioFile() => null;
        public bool Confirm(string titulo, string mensagem) { Perguntou = true; return Resposta; }
        public void Info(string titulo, string mensagem) { }
        public bool ShowAudioEditor(AudioEditViewModel viewModel) => false;
    }
}
