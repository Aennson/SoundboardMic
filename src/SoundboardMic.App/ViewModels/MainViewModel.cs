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
        MyInstantsViewModel myInstantsViewModel,
        EditorDeSonsViewModel editorDeSonsViewModel)
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
        EditorDeSons = editorDeSonsViewModel;
        EditorDeSons.Vincular(Audios);
        EditorDeSons.ReportarErro = MostrarErro;
        EditorDeSons.AposAlteracao = AposCorteAsync;

        _controller.StatusChanged += (_, _) => _dispatcher.Invoke(UpdateStatus);
        _controller.ErrorRaised += (_, msg) => _dispatcher.Invoke(() => MostrarErro(msg));
    }

    public SettingsViewModel Settings { get; }
    public MyInstantsViewModel MyInstants { get; }
    public EditorDeSonsViewModel EditorDeSons { get; }

    public ObservableCollection<AudioItemViewModel> Audios { get; } = new();

    /// <summary>Seções por categoria já filtradas/ordenadas, prontas para exibição em "Meus sons".</summary>
    public ObservableCollection<CategoriaGroupViewModel> Grupos { get; } = new();

    /// <summary>Aba principal exibida: "sons" (acervo), "web" (myinstants), "editor" (corte) ou "config".</summary>
    [ObservableProperty] private string _aba = "sons";

    public bool MostrandoSons => Aba == "sons";
    public bool MostrandoMyInstants => Aba == "web";
    public bool MostrandoEditor => Aba == "editor";
    public bool MostrandoConfiguracoes => Aba == "config";

    partial void OnAbaChanged(string value)
    {
        OnPropertyChanged(nameof(MostrandoSons));
        OnPropertyChanged(nameof(MostrandoMyInstants));
        OnPropertyChanged(nameof(MostrandoEditor));
        OnPropertyChanged(nameof(MostrandoConfiguracoes));
        if (value != "editor")
            EditorDeSons.PararPreview();
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

        // Reaproveita os cards já exibidos: trocar o modelo em vez de recriar o view model
        // mantém os containers da grade vivos, evitando reanimar a lista inteira.
        var anteriores = Audios.ToDictionary(a => a.Id);
        var nomesCategoria = _categorias.ToDictionary(c => c.Id, c => c.Nome);
        var atualizados = new List<AudioItemViewModel>(audios.Count);
        foreach (var audio in audios)
        {
            mapeamentos.TryGetValue(audio.Id, out var map);
            if (anteriores.TryGetValue(audio.Id, out var existente))
                existente.Atualizar(audio, map);
            else
                existente = new AudioItemViewModel(audio, map);
            existente.CategoriaNome = audio.CategoriaId is long cid && nomesCategoria.TryGetValue(cid, out var nomeCat)
                ? nomeCat
                : "Sem categoria";
            atualizados.Add(existente);
        }

        GradeSync.Sincronizar(Audios, atualizados);
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

        var desejados = new List<GradeSync.GrupoDesejado>();

        foreach (var categoria in _categorias.OrderBy(c => c.Ordem))
        {
            var itens = porCategoria[categoria.Id].OrderBy(a => a.Audio.Ordem).ThenBy(a => a.Audio.CriadoEm).ToList();
            if (itens.Count == 0)
                continue;

            desejados.Add(new GradeSync.GrupoDesejado(categoria.Id, categoria.Nome, categoria.Ordem, itens));
        }

        var semCategoria = porCategoria[null].OrderBy(a => a.Audio.Ordem).ThenBy(a => a.Audio.CriadoEm).ToList();
        if (semCategoria.Count > 0)
            desejados.Add(new GradeSync.GrupoDesejado(null, "Sem categoria", int.MaxValue, semCategoria));

        GradeSync.SincronizarGrupos(Grupos, desejados);

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
        var loops = new HashSet<string>(status.LoopingSoundPaths, StringComparer.OrdinalIgnoreCase);
        foreach (var item in Audios)
        {
            item.Tocando = tocando.Contains(item.CaminhoArquivo);
            item.EmLoop = loops.Contains(item.CaminhoArquivo);
        }
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
    [RelayCommand] private void MostrarEditor() => Aba = "editor";
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
        var id = item.Id;
        var vm = CriarEditor(item);
        if (!_dialogs.ShowAudioEditor(vm) || (!vm.Salvou && !vm.Excluiu))
            return;

        await RecarregarTudoAsync();

        // Só o card editado reanima a entrada; os demais containers foram reaproveitados.
        if (vm.Salvou && Audios.FirstOrDefault(a => a.Id == id) is { } editado)
            await ReanimarEntradaAsync(editado);
    }

    /// <summary>Duração da reanimação de entrada do card editado (ver ItemContainerStyle em SoundboardView).</summary>
    private static readonly TimeSpan DuracaoEntrada = TimeSpan.FromMilliseconds(260);

    private async Task ReanimarEntradaAsync(AudioItemViewModel item)
    {
        item.Entrando = true;
        await Task.Delay(DuracaoEntrada);
        item.Entrando = false;
    }

    /// <summary>Abre o Editor de sons já com este áudio selecionado.</summary>
    [RelayCommand]
    private void CortarAudio(AudioItemViewModel? item)
    {
        if (item is null) return;
        Aba = "editor";
        EditorDeSons.Abrir(item);
    }

    /// <summary>Depois de um corte salvo: recarrega e anima só o card criado/substituído.</summary>
    private async Task AposCorteAsync(long audioId)
    {
        await RecarregarTudoAsync();
        if (Audios.FirstOrDefault(a => a.Id == audioId) is { } afetado)
            _ = ReanimarEntradaAsync(afetado);
    }

    [RelayCommand]
    private async Task ExcluirAudio(AudioItemViewModel? item)
    {
        if (item is null) return;
        if (!_dialogs.Confirm("Excluir áudio",
                $"Remover \"{item.Nome}\"? O atalho associado também será removido."))
            return;

        try
        {
            _controller.StopSound(item.CaminhoArquivo);
            await _audioRepo.DeleteAsync(item.Id); // cascade remove o mapeamento
            _fileCache.RemoverCache(item.CaminhoArquivo);
            await RemoverComAnimacaoAsync(item);
            await _controller.ReloadBindingsAsync();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MostrarErro($"Não foi possível excluir o áudio: {ex.Message}");
        }
    }

    /// <summary>Duração da animação de saída do card (ver ItemContainerStyle em SoundboardView).</summary>
    private static readonly TimeSpan DuracaoRemocao = TimeSpan.FromMilliseconds(300);

    private async Task RemoverComAnimacaoAsync(AudioItemViewModel item)
    {
        await CardRemoval.RemoverAsync(Grupos, Audios, item, DuracaoRemocao);
        ListaVazia = Audios.Count == 0;
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

    [RelayCommand]
    private void AlternarLoopAudio(AudioItemViewModel? item)
    {
        if (item is null) return;
        _controller.ToggleLoopSound(item.CaminhoArquivo, (float)item.Audio.VolumePadrao);
        UpdateStatus();
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

    /// <summary>Indica se o card pode ser solto sobre outro: só dentro da mesma categoria.</summary>
    public bool PodeMoverAudio(AudioItemViewModel origem, AudioItemViewModel destino) =>
        !ReferenceEquals(origem, destino) && Grupos.Any(g => g.Itens.Contains(origem) && g.Itens.Contains(destino));

    /// <summary>Arrastar e soltar: move o card para a posição do destino e persiste a nova ordem.</summary>
    public async Task MoverAudioParaAsync(AudioItemViewModel origem, AudioItemViewModel destino)
    {
        var grupo = Grupos.FirstOrDefault(g => g.Itens.Contains(origem) && g.Itens.Contains(destino));
        if (grupo is null) return;

        try
        {
            foreach (var item in AudioOrdering.Mover(grupo.Itens, origem, destino))
                await _audioRepo.UpdateAsync(item.Audio);
        }
        catch (Exception ex)
        {
            MostrarErro($"Não foi possível salvar a nova ordem: {ex.Message}");
            ReconstruirGrupos();
        }
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
