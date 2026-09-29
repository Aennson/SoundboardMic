using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardMic.App.Services;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// Aba "Editor de sons": escolhe um áudio do acervo, mostra a forma de onda, deixa selecionar
/// um trecho contínuo, ouvir só esse trecho nos fones e salvá-lo como áudio novo ou por cima
/// do original.
/// </summary>
public partial class EditorDeSonsViewModel : ObservableObject, IDisposable
{
    private readonly IPlaybackService _player;
    private readonly AudioTrimService _trim;
    private readonly SoundboardController _controller;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _cursorTimer;
    private CancellationTokenSource? _ondaCts;
    private bool _normalizando;

    public EditorDeSonsViewModel(
        IPlaybackService player,
        AudioTrimService trim,
        SoundboardController controller,
        ISettingsService settings,
        IDialogService dialogs)
    {
        _player = player;
        _trim = trim;
        _controller = controller;
        _settings = settings;
        _dialogs = dialogs;

        _cursorTimer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _cursorTimer.Tick += (_, _) => AtualizarCursor();
        _player.PlaybackStopped += (_, _) => _dispatcher.BeginInvoke(PreviewTerminou);
    }

    /// <summary>
    /// Chamado pelo <see cref="MainViewModel"/> após criar ou substituir um áudio: recarrega a
    /// lista e anima só o card afetado. Recebe o Id do áudio resultante.
    /// </summary>
    public Func<long, Task>? AposAlteracao { get; set; }

    /// <summary>Recebe os erros para o banner padrão do app.</summary>
    public Action<string>? ReportarErro { get; set; }

    private ObservableCollection<AudioItemViewModel>? _audios;

    /// <summary>Lista pesquisável: view própria sobre a mesma coleção de "Meus sons".</summary>
    public ICollectionView? AudiosView { get; private set; }

    /// <summary>Liga o editor à coleção de áudios da janela principal.</summary>
    public void Vincular(ObservableCollection<AudioItemViewModel> audios)
    {
        if (_audios is not null)
            _audios.CollectionChanged -= AoMudarAudios;
        _audios = audios;
        _audios.CollectionChanged += AoMudarAudios;

        var view = new ListCollectionView(audios) { Filter = Corresponde };
        view.SortDescriptions.Add(new SortDescription(nameof(AudioItemViewModel.Nome), ListSortDirection.Ascending));
        view.IsLiveSorting = true;
        view.LiveSortingProperties.Add(nameof(AudioItemViewModel.Nome));
        AudiosView = view;
        OnPropertyChanged(nameof(AudiosView));
        AtualizarVazio();
    }

    private void AoMudarAudios(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        AtualizarVazio();
        // O áudio aberto foi excluído em outra aba: limpa o editor.
        if (Selecionado is not null && _audios is not null && !_audios.Contains(Selecionado))
            Selecionado = null;
    }

    private void AtualizarVazio() => SemAudios = _audios is null || _audios.Count == 0;

    // ---- Busca ----

    [ObservableProperty] private string _busca = string.Empty;
    [ObservableProperty] private bool _semAudios = true;

    partial void OnBuscaChanged(string value) => AudiosView?.Refresh();

    internal bool Corresponde(object obj)
    {
        if (obj is not AudioItemViewModel item) return false;
        var termo = Busca?.Trim();
        if (string.IsNullOrEmpty(termo)) return true;
        return CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            item.Nome, termo, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    }

    // ---- Seleção de áudio / onda ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSelecionado))]
    [NotifyCanExecuteChangedFor(nameof(OuvirSelecaoCommand), nameof(SelecionarTudoCommand),
        nameof(CriarNovoCommand), nameof(SubstituirCommand))]
    private AudioItemViewModel? _selecionado;

    public bool TemSelecionado => Selecionado is not null;

    [ObservableProperty] private float[]? _picos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuracaoTotalTexto))]
    [NotifyCanExecuteChangedFor(nameof(OuvirSelecaoCommand), nameof(SelecionarTudoCommand),
        nameof(CriarNovoCommand), nameof(SubstituirCommand))]
    private TimeSpan _duracao;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OuvirSelecaoCommand), nameof(SelecionarTudoCommand),
        nameof(CriarNovoCommand), nameof(SubstituirCommand))]
    private bool _carregandoOnda;

    public string DuracaoTotalTexto => SelecaoCorte.Formatar(Duracao);

    partial void OnSelecionadoChanged(AudioItemViewModel? value)
    {
        PararPreview();
        Mensagem = null;
        NomeNovo = value is null ? string.Empty : AudioTrimService.NomeSugerido(value.Nome);
        _ = CarregarOndaAsync(value);
    }

    /// <summary>Seleciona o áudio pelo Id (usado pelo botão "Cortar" do card).</summary>
    public void Abrir(AudioItemViewModel item)
    {
        if (!string.IsNullOrEmpty(Busca) && !Corresponde(item))
            Busca = string.Empty;
        if (ReferenceEquals(Selecionado, item))
            _ = CarregarOndaAsync(item);
        else
            Selecionado = item;
    }

    private async Task CarregarOndaAsync(AudioItemViewModel? item)
    {
        _ondaCts?.Cancel();
        _ondaCts?.Dispose();
        _ondaCts = null;

        DefinirSelecao(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        Picos = null;
        if (item is null)
        {
            CarregandoOnda = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _ondaCts = cts;
        CarregandoOnda = true;
        var caminho = item.CaminhoArquivo;
        try
        {
            var onda = await Task.Run(() => WaveformPeaks.Calcular(caminho, WaveformPeaks.ColunasPadrao, cts.Token), cts.Token);
            if (cts.IsCancellationRequested) return;
            Picos = onda.Picos;
            DefinirSelecao(onda.Duracao, TimeSpan.Zero, onda.Duracao);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
                ReportarErro?.Invoke($"Não foi possível abrir \"{item.Nome}\": {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_ondaCts, cts))
                CarregandoOnda = false;
        }
    }

    // ---- Trecho ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuracaoSelecaoTexto))]
    [NotifyCanExecuteChangedFor(nameof(CriarNovoCommand), nameof(SubstituirCommand))]
    private TimeSpan _inicio;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuracaoSelecaoTexto))]
    [NotifyCanExecuteChangedFor(nameof(CriarNovoCommand), nameof(SubstituirCommand))]
    private TimeSpan _fim;

    [ObservableProperty] private string _inicioTexto = SelecaoCorte.Formatar(TimeSpan.Zero);
    [ObservableProperty] private string _fimTexto = SelecaoCorte.Formatar(TimeSpan.Zero);

    /// <summary>Posição da pré-escuta na onda (null = parado).</summary>
    [ObservableProperty] private TimeSpan? _cursor;

    public string DuracaoSelecaoTexto => SelecaoCorte.Formatar(Fim - Inicio);

    public SelecaoCorte Selecao => new(Inicio, Fim);

    private void DefinirSelecao(TimeSpan duracao, TimeSpan inicio, TimeSpan fim)
    {
        _normalizando = true;
        try
        {
            Duracao = duracao;
            Fim = fim;
            Inicio = inicio;
            InicioTexto = SelecaoCorte.Formatar(inicio);
            FimTexto = SelecaoCorte.Formatar(fim);
        }
        finally
        {
            _normalizando = false;
        }
    }

    partial void OnInicioChanged(TimeSpan value)
    {
        if (_normalizando) return;
        _normalizando = true;
        try
        {
            var ajustado = new SelecaoCorte(value, Fim).ComInicio(value, Duracao);
            Inicio = ajustado.Inicio;
            InicioTexto = SelecaoCorte.Formatar(Inicio);
        }
        finally
        {
            _normalizando = false;
        }
        PararPreviewSeTocando();
    }

    partial void OnFimChanged(TimeSpan value)
    {
        if (_normalizando) return;
        _normalizando = true;
        try
        {
            var ajustado = new SelecaoCorte(Inicio, value).ComFim(value, Duracao);
            Fim = ajustado.Fim;
            FimTexto = SelecaoCorte.Formatar(Fim);
        }
        finally
        {
            _normalizando = false;
        }
        PararPreviewSeTocando();
    }

    partial void OnInicioTextoChanged(string value)
    {
        if (_normalizando) return;
        if (SelecaoCorte.TryParse(value, out var t))
            Inicio = t;
        // Reescreve no formato padrão (ou desfaz o texto inválido).
        _normalizando = true;
        InicioTexto = SelecaoCorte.Formatar(Inicio);
        _normalizando = false;
    }

    partial void OnFimTextoChanged(string value)
    {
        if (_normalizando) return;
        if (SelecaoCorte.TryParse(value, out var t))
            Fim = t;
        _normalizando = true;
        FimTexto = SelecaoCorte.Formatar(Fim);
        _normalizando = false;
    }

    private bool PodeEditar() => Selecionado is not null && !CarregandoOnda && !Ocupado && Duracao > TimeSpan.Zero;

    [RelayCommand(CanExecute = nameof(PodeEditar))]
    private void SelecionarTudo() => DefinirSelecao(Duracao, TimeSpan.Zero, Duracao);

    // ---- Pré-escuta ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OuvirTexto), nameof(OuvirGlifo))]
    private bool _ouvindo;

    public string OuvirTexto => Ouvindo ? "Parar" : "Ouvir seleção";
    public string OuvirGlifo => Ouvindo ? "\uE71A" : "\uE768";

    [RelayCommand(CanExecute = nameof(PodeEditar))]
    private void OuvirSelecao()
    {
        if (Ouvindo)
        {
            PararPreview();
            return;
        }
        if (Selecionado is null) return;

        try
        {
            var ganho = VolumeCurve.ToGain((float)Selecionado.Audio.VolumePadrao)
                        * VolumeCurve.ToGain(_settings.Current.SoundboardVolume);
            _player.PlayRange(Selecionado.CaminhoArquivo, Inicio, Fim, _settings.Current.MonitorDeviceId, ganho);
            Ouvindo = true;
            Cursor = Inicio;
            _cursorTimer.Start();
        }
        catch (Exception ex)
        {
            PararPreview();
            ReportarErro?.Invoke($"Não foi possível tocar o trecho: {ex.Message}");
        }
    }

    private void AtualizarCursor()
    {
        if (!Ouvindo) return;
        Cursor = _player.Position;
    }

    private void PreviewTerminou()
    {
        _cursorTimer.Stop();
        Ouvindo = false;
        Cursor = null;
    }

    private void PararPreviewSeTocando()
    {
        if (Ouvindo) PararPreview();
    }

    /// <summary>Interrompe a pré-escuta (ao trocar de áudio, de aba ou salvar).</summary>
    public void PararPreview()
    {
        _cursorTimer.Stop();
        if (_player.IsPlaying) _player.Stop();
        Ouvindo = false;
        Cursor = null;
    }

    // ---- Salvar ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OuvirSelecaoCommand), nameof(SelecionarTudoCommand),
        nameof(CriarNovoCommand), nameof(SubstituirCommand))]
    private bool _ocupado;

    [ObservableProperty] private string _nomeNovo = string.Empty;

    /// <summary>Aviso discreto de sucesso ("Novo áudio criado", "Áudio substituído").</summary>
    [ObservableProperty] private string? _mensagem;

    private bool PodeSalvar() =>
        PodeEditar() && Fim - Inicio >= MinimoPara(Duracao) && !Selecao.CobreTudo(Duracao);

    private static TimeSpan MinimoPara(TimeSpan duracao) =>
        duracao < AudioTrimmer.DuracaoMinima ? duracao : AudioTrimmer.DuracaoMinima;

    [RelayCommand(CanExecute = nameof(PodeSalvar))]
    private async Task CriarNovo()
    {
        if (Selecionado is not { } item) return;
        PararPreview();
        Ocupado = true;
        Mensagem = null;
        try
        {
            var (caminho, inicio, fim) = (item.CaminhoArquivo, Inicio, Fim);
            var wav = await Task.Run(() => AudioTrimmer.CortarParaWav(caminho, inicio, fim));
            var novo = await _trim.CriarNovoAsync(item.Audio, wav, NomeNovo);
            if (AposAlteracao is not null)
                await AposAlteracao(novo.Id);
            Mensagem = $"Novo áudio criado: \"{novo.Nome}\".";
        }
        catch (Exception ex)
        {
            ReportarErro?.Invoke($"Não foi possível criar o novo áudio: {ex.Message}");
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PodeSalvar))]
    private async Task Substituir()
    {
        if (Selecionado is not { } item) return;
        if (!_dialogs.Confirm("Substituir áudio original",
                $"Substituir \"{item.Nome}\" pelo trecho selecionado? " +
                "Esta ação sobrescreve o áudio e não pode ser desfeita."))
            return;

        PararPreview();
        Ocupado = true;
        Mensagem = null;
        try
        {
            var (caminho, inicio, fim) = (item.CaminhoArquivo, Inicio, Fim);
            var wav = await Task.Run(() => AudioTrimmer.CortarParaWav(caminho, inicio, fim));
            // O cache antigo é apagado ao final: nada pode estar tocando esse arquivo.
            _controller.StopSound(caminho);
            await _trim.SubstituirAsync(item.Audio, wav);
            if (AposAlteracao is not null)
                await AposAlteracao(item.Id);
            await CarregarOndaAsync(item);
            Mensagem = "Áudio substituído.";
        }
        catch (Exception ex)
        {
            ReportarErro?.Invoke($"Não foi possível substituir o áudio: {ex.Message}");
        }
        finally
        {
            Ocupado = false;
        }
    }

    public void Dispose()
    {
        _cursorTimer.Stop();
        _ondaCts?.Cancel();
        _ondaCts?.Dispose();
        if (_audios is not null)
            _audios.CollectionChanged -= AoMudarAudios;
    }
}
