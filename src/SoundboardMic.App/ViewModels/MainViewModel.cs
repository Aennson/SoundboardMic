using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SoundboardMic.App.Services;
using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.App.ViewModels;

/// <summary>ViewModel raiz da janela principal: lista, status, navegação e comandos.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly IAudioRepository _audioRepo;
    private readonly IMapeamentoRepository _mapeamentoRepo;
    private readonly ICategoriaRepository _categoriaRepo;
    private readonly IDialogService _dialogs;
    private readonly SoundboardController _controller;
    private readonly ISettingsService _settings;
    private readonly QuickBarService _quickBar;
    private readonly AudioFileCache _fileCache;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
    private IReadOnlyList<Categoria> _categorias = Array.Empty<Categoria>();

    public MainViewModel(
        IServiceProvider services,
        IAudioRepository audioRepo,
        IMapeamentoRepository mapeamentoRepo,
        ICategoriaRepository categoriaRepo,
        IDialogService dialogs,
        SoundboardController controller,
        ISettingsService settings,
        QuickBarService quickBar,
        AudioFileCache fileCache,
        SettingsViewModel settingsViewModel,
        MyInstantsViewModel myInstantsViewModel)
    {
        _services = services;
        _audioRepo = audioRepo;
        _mapeamentoRepo = mapeamentoRepo;
        _categoriaRepo = categoriaRepo;
        _dialogs = dialogs;
        _controller = controller;
        _settings = settings;
        _quickBar = quickBar;
        _fileCache = fileCache;
        Settings = settingsViewModel;
        MyInstants = myInstantsViewModel;
        MyInstants.SomAdicionado += (_, _) => _dispatcher.InvokeAsync(async () => await RecarregarTudoAsync());

        _controller.StatusChanged += (_, _) => _dispatcher.Invoke(UpdateStatus);
        _controller.ErrorRaised += (_, msg) => _dispatcher.Invoke(() => MostrarErro(msg));
    }

    public SettingsViewModel Settings { get; }
    public MyInstantsViewModel MyInstants { get; }

    public ObservableCollection<AudioItemViewModel> Audios { get; } = new();

    /// <summary>Seções por categoria já filtradas/ordenadas, prontas para exibição em "Meus sons".</summary>
    public ObservableCollection<CategoriaGroupViewModel> Grupos { get; } = new();

    /// <summary>Aba principal exibida: "sons" (acervo), "web" (myinstants) ou "config".</summary>
    [ObservableProperty] private string _aba = "sons";

    public bool MostrandoSons => Aba == "sons";
    public bool MostrandoMyInstants => Aba == "web";
    public bool MostrandoConfiguracoes => Aba == "config";

    partial void OnAbaChanged(string value)
    {
        OnPropertyChanged(nameof(MostrandoSons));
        OnPropertyChanged(nameof(MostrandoMyInstants));
        OnPropertyChanged(nameof(MostrandoConfiguracoes));
        if (value == "web")
            _ = MyInstants.CarregarInicialAsync();
        else
            MyInstants.PararPreview();
    }

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

    partial void OnFiltroChanged(string value) => ReconstruirGrupos();

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
        _categorias = await _categoriaRepo.GetAllAsync();
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
        ReconstruirGrupos();
    }

    /// <summary>Reconstrói as seções por categoria a partir de <see cref="Audios"/>, aplicando o
    /// filtro de busca atual. Categorias ficam na ordem definida por <see cref="Categoria.Ordem"/>;
    /// a seção "sem categoria" sempre é exibida por último.</summary>
    private void ReconstruirGrupos()
    {
        var termo = Filtro;
        bool Corresponde(AudioItemViewModel vm) =>
            string.IsNullOrWhiteSpace(termo) || vm.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase);

        // ILookup aceita chave nula (ao contrário de Dictionary<long?, ...>), o que
        // simplifica agrupar categorizados e "sem categoria" (chave null) de uma vez.
        var porCategoria = Audios.Where(Corresponde).ToLookup(a => a.Audio.CategoriaId);

        Grupos.Clear();

        foreach (var categoria in _categorias.OrderBy(c => c.Ordem))
        {
            var itens = porCategoria[categoria.Id].OrderBy(a => a.Audio.Ordem).ThenBy(a => a.Audio.CriadoEm).ToList();
            if (itens.Count == 0)
                continue;

            var grupo = new CategoriaGroupViewModel(categoria.Id, categoria.Nome, categoria.Ordem);
            foreach (var item in itens)
                grupo.Itens.Add(item);
            Grupos.Add(grupo);
        }

        var semCategoria = porCategoria[null].OrderBy(a => a.Audio.Ordem).ThenBy(a => a.Audio.CriadoEm).ToList();
        if (semCategoria.Count > 0)
        {
            var grupo = new CategoriaGroupViewModel(null, "Sem categoria", int.MaxValue);
            foreach (var item in semCategoria)
                grupo.Itens.Add(item);
            Grupos.Add(grupo);
        }

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
    [RelayCommand] private void MostrarSons() => Aba = "sons";
    [RelayCommand] private void MostrarMyInstants() => Aba = "web";
    [RelayCommand] private void MostrarConfig() => Aba = "config";

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

    // ---- Organização por categoria ----
    [RelayCommand]
    private async Task MoverCategoriaAcima(CategoriaGroupViewModel? grupo)
    {
        if (grupo?.CategoriaId is not long id) return;
        var ordenadas = Grupos.Where(g => g.CategoriaId is not null).OrderBy(g => g.Ordem).ToList();
        var idx = ordenadas.FindIndex(g => g.CategoriaId == id);
        if (idx <= 0) return;
        await TrocarOrdemCategoriasAsync(ordenadas, idx, idx - 1);
    }

    [RelayCommand]
    private async Task MoverCategoriaAbaixo(CategoriaGroupViewModel? grupo)
    {
        if (grupo?.CategoriaId is not long id) return;
        var ordenadas = Grupos.Where(g => g.CategoriaId is not null).OrderBy(g => g.Ordem).ToList();
        var idx = ordenadas.FindIndex(g => g.CategoriaId == id);
        if (idx < 0 || idx >= ordenadas.Count - 1) return;
        await TrocarOrdemCategoriasAsync(ordenadas, idx, idx + 1);
    }

    /// <summary>Renumera as categorias sequencialmente (0..n-1) na ordem atual e então
    /// troca as duas posições alvo, persistindo tudo. A renumeração evita que categorias
    /// com <see cref="Categoria.Ordem"/> empatado (ex.: criadas na mesma sessão) deixem
    /// a troca sem efeito visual.</summary>
    private async Task TrocarOrdemCategoriasAsync(List<CategoriaGroupViewModel> ordenadas, int idxA, int idxB)
    {
        for (var i = 0; i < ordenadas.Count; i++)
            ordenadas[i].Ordem = i;
        (ordenadas[idxA].Ordem, ordenadas[idxB].Ordem) = (ordenadas[idxB].Ordem, ordenadas[idxA].Ordem);

        foreach (var grupo in ordenadas)
            await _categoriaRepo.UpdateAsync(new Categoria { Id = grupo.CategoriaId!.Value, Nome = grupo.Nome, Ordem = grupo.Ordem });

        _categorias = await _categoriaRepo.GetAllAsync();
        ReconstruirGrupos();
    }

    [RelayCommand]
    private async Task MoverAudioAcima(AudioItemViewModel? item)
    {
        if (item is null) return;
        var grupo = Grupos.FirstOrDefault(g => g.Itens.Contains(item));
        if (grupo is null) return;
        var idx = grupo.Itens.IndexOf(item);
        if (idx <= 0) return;
        await TrocarOrdemAudiosAsync(grupo.Itens, idx, idx - 1);
    }

    [RelayCommand]
    private async Task MoverAudioAbaixo(AudioItemViewModel? item)
    {
        if (item is null) return;
        var grupo = Grupos.FirstOrDefault(g => g.Itens.Contains(item));
        if (grupo is null) return;
        var idx = grupo.Itens.IndexOf(item);
        if (idx < 0 || idx >= grupo.Itens.Count - 1) return;
        await TrocarOrdemAudiosAsync(grupo.Itens, idx, idx + 1);
    }

    /// <summary>Renumera os áudios da categoria sequencialmente (0..n-1) na ordem atual
    /// e então troca as duas posições alvo, persistindo tudo. Evita que áudios com
    /// <see cref="Audio.Ordem"/> empatado (ex.: adicionados na mesma sessão) deixem a
    /// troca sem efeito visual.</summary>
    private async Task TrocarOrdemAudiosAsync(ObservableCollection<AudioItemViewModel> itens, int idxA, int idxB)
    {
        for (var i = 0; i < itens.Count; i++)
            itens[i].Audio.Ordem = i;
        (itens[idxA].Audio.Ordem, itens[idxB].Audio.Ordem) = (itens[idxB].Audio.Ordem, itens[idxA].Audio.Ordem);

        foreach (var item in itens)
            await _audioRepo.UpdateAsync(item.Audio);

        ReconstruirGrupos();
    }

    private AudioEditViewModel CriarEditor(AudioItemViewModel? editing) => new(
        _audioRepo,
        _mapeamentoRepo,
        _categoriaRepo,
        _dialogs,
        _services.GetRequiredService<Core.AudioEngine.IPlaybackService>(),
        _settings,
        _fileCache,
        _categorias,
        editing);

    private async Task RecarregarTudoAsync()
    {
        await CarregarAudiosAsync();
        await _controller.ReloadBindingsAsync();
        UpdateStatus();
    }
}
