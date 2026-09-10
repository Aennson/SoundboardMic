using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SoundboardMic.App.Services;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.App.ViewModels;

/// <summary>ViewModel raiz da janela principal: lista, status, navegação e comandos.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly IAudioRepository _audioRepo;
    private readonly IMapeamentoRepository _mapeamentoRepo;
    private readonly IDialogService _dialogs;
    private readonly SoundboardController _controller;
    private readonly ISettingsService _settings;
    private readonly QuickBarService _quickBar;
    private readonly AudioFileCache _fileCache;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    public MainViewModel(
        IServiceProvider services,
        IAudioRepository audioRepo,
        IMapeamentoRepository mapeamentoRepo,
        IDialogService dialogs,
        SoundboardController controller,
        ISettingsService settings,
        QuickBarService quickBar,
        AudioFileCache fileCache,
        SettingsViewModel settingsViewModel)
    {
        _services = services;
        _audioRepo = audioRepo;
        _mapeamentoRepo = mapeamentoRepo;
        _dialogs = dialogs;
        _controller = controller;
        _settings = settings;
        _quickBar = quickBar;
        _fileCache = fileCache;
        Settings = settingsViewModel;

        _controller.StatusChanged += (_, _) => _dispatcher.Invoke(UpdateStatus);
        _controller.ErrorRaised += (_, msg) => _dispatcher.Invoke(() => MostrarErro(msg));
    }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<AudioItemViewModel> Audios { get; } = new();

    [ObservableProperty] private bool _mostrandoConfiguracoes;

    [ObservableProperty] private string _filtro = string.Empty;

    // ---- Status (cabeçalho) ----
    [ObservableProperty] private bool _engineRunning;
    [ObservableProperty] private bool _cableDetected;
    [ObservableProperty] private int _activeSounds;
    [ObservableProperty] private string _outputDeviceName = "—";
    [ObservableProperty] private string _micDeviceName = "—";

    [ObservableProperty] private string? _erroBanner;
    [ObservableProperty] private bool _listaVazia;

    public string EngineButtonGlyph => EngineRunning ? "" : ""; // Stop / Play
    public string EngineButtonTexto => EngineRunning ? "Parar injeção" : "Iniciar injeção";

    partial void OnEngineRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(EngineButtonGlyph));
        OnPropertyChanged(nameof(EngineButtonTexto));
    }

    partial void OnFiltroChanged(string value) => AplicarFiltro();

    /// <summary>Carrega áudios + mapeamentos, instala hook e (opcional) inicia o motor.</summary>
    public async Task InitializeAsync()
    {
        await CarregarAudiosAsync();

        _controller.EnsureHookInstalled();
        await _controller.ReloadBindingsAsync();

        if (_settings.Current.AutoStartEngine)
            _controller.StartEngine();

        UpdateStatus();
    }

    private async Task CarregarAudiosAsync()
    {
        var audios = await _audioRepo.GetAllAsync();
        var mapeamentos = (await _mapeamentoRepo.GetAllAsync())
            .GroupBy(m => m.AudioId)
            .ToDictionary(g => g.Key, g => g.First());

        // Garante que todo áudio tenha seu conteúdo salvo no banco (migra registros
        // legados que só tinham o caminho externo) e que o cache em disco exista
        // (regenera a partir do BLOB se tiver sido apagado/movido de máquina).
        foreach (var audio in audios)
        {
            var migrado = await _fileCache.MigrarLegadoAsync(audio);
            var materializado = !migrado && _fileCache.GarantirMaterializado(audio);
            if (migrado || materializado)
                await _audioRepo.UpdateAsync(audio);
        }

        Audios.Clear();
        foreach (var audio in audios)
        {
            mapeamentos.TryGetValue(audio.Id, out var map);
            Audios.Add(new AudioItemViewModel(audio, map));
        }
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Audios);
        if (view is null) return;
        view.Filter = string.IsNullOrWhiteSpace(Filtro)
            ? null
            : o => o is AudioItemViewModel vm
                   && vm.Nome.Contains(Filtro, StringComparison.OrdinalIgnoreCase);
        ListaVazia = Audios.Count == 0;
    }

    private void UpdateStatus()
    {
        var status = _controller.GetStatus();
        EngineRunning = status.EngineRunning;
        CableDetected = status.CableDetected;
        ActiveSounds = status.ActiveSounds;
        OutputDeviceName = status.OutputDeviceName ?? "—";
        MicDeviceName = status.MicDeviceName ?? "—";

        var tocando = new HashSet<string>(status.ActiveSoundPaths, StringComparer.OrdinalIgnoreCase);
        foreach (var item in Audios)
            item.Tocando = tocando.Contains(item.CaminhoArquivo);
    }

    private void MostrarErro(string mensagem)
    {
        ErroBanner = mensagem;
        // Auto-oculta após alguns segundos.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        timer.Tick += (_, _) => { ErroBanner = null; timer.Stop(); };
        timer.Start();
    }

    // ---- Navegação ----
    [RelayCommand] private void MostrarSons() => MostrandoConfiguracoes = false;
    [RelayCommand] private void MostrarConfig() => MostrandoConfiguracoes = true;

    [RelayCommand] private void FecharErro() => ErroBanner = null;

    // ---- Motor ----
    [RelayCommand]
    private void ToggleEngine()
    {
        if (_controller.IsEngineRunning)
            _controller.StopEngine();
        else
            _controller.StartEngine();
    }

    [RelayCommand]
    private void AlternarQuickBar() => _quickBar.Alternar();

    [RelayCommand]
    private void Panico() => _controller.StopAllSounds();
    // O stop-all dispara ActiveSoundsChanged → UpdateStatus limpa os flags Tocando.

    // ---- CRUD de áudios ----
    [RelayCommand]
    private async Task AdicionarAudio()
    {
        var vm = CriarEditor(null);
        if (_dialogs.ShowAudioEditor(vm) && vm.Salvou)
            await RecarregarTudoAsync();
    }

    [RelayCommand]
    private async Task EditarAudio(AudioItemViewModel? item)
    {
        if (item is null) return;
        var vm = CriarEditor(item);
        if (_dialogs.ShowAudioEditor(vm) && (vm.Salvou || vm.Excluiu))
            await RecarregarTudoAsync();
    }

    [RelayCommand]
    private void TocarAudio(AudioItemViewModel? item)
    {
        if (item is null) return;
        // Sempre dispara uma nova instância: o mesmo som pode se sobrepor várias vezes.
        _controller.TriggerSound(item.CaminhoArquivo, (float)item.Audio.VolumePadrao);
    }

    [RelayCommand]
    private void PararAudio(AudioItemViewModel? item)
    {
        if (item is null) return;
        _controller.StopSound(item.CaminhoArquivo);
    }

    private AudioEditViewModel CriarEditor(AudioItemViewModel? editing) => new(
        _audioRepo,
        _mapeamentoRepo,
        _dialogs,
        _services.GetRequiredService<Core.AudioEngine.IPlaybackService>(),
        _settings,
        _fileCache,
        editing);

    private async Task RecarregarTudoAsync()
    {
        await CarregarAudiosAsync();
        await _controller.ReloadBindingsAsync();
        UpdateStatus();
    }
}
