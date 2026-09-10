using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardMic.App.Services;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Hotkeys;
using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// ViewModel do editor de áudio (novo ou edição): nome, arquivo, volume, preview e
/// gravação de atalho, com validação de conflito de teclas.
/// </summary>
public partial class AudioEditViewModel : ObservableObject
{
    private readonly IAudioRepository _audioRepo;
    private readonly IMapeamentoRepository _mapeamentoRepo;
    private readonly IDialogService _dialogs;
    private readonly IPlaybackService _preview;
    private readonly ISettingsService _settings;
    private readonly AudioFileCache _fileCache;

    private readonly Audio? _existing;
    private readonly Mapeamento? _existingMapeamento;

    /// <summary>True quando o usuário escolheu um arquivo novo nesta sessão de edição
    /// (via "Procurar"), indicando que o conteúdo deve ser reimportado para o banco.</summary>
    private bool _arquivoAlterado;

    public AudioEditViewModel(
        IAudioRepository audioRepo,
        IMapeamentoRepository mapeamentoRepo,
        IDialogService dialogs,
        IPlaybackService preview,
        ISettingsService settings,
        AudioFileCache fileCache,
        AudioItemViewModel? editing)
    {
        _audioRepo = audioRepo;
        _mapeamentoRepo = mapeamentoRepo;
        _dialogs = dialogs;
        _preview = preview;
        _settings = settings;
        _fileCache = fileCache;

        _existing = editing?.Audio;
        _existingMapeamento = editing?.Mapeamento;

        if (editing is not null)
        {
            _nome = editing.Audio.Nome;
            _caminhoArquivo = editing.Audio.CaminhoArquivo;
            _volume = editing.Audio.VolumePadrao;
            _teclas = editing.Mapeamento?.Teclas ?? string.Empty;
        }

        // Null (legado/novo) vira o padrão do catálogo — a UI sempre tem uma seleção.
        _icone = editing?.Audio.Icone ?? IconCatalog.GlifoPadrao;
        _cor = editing?.Audio.Cor ?? IconCatalog.CorPadrao;
        _ativo = editing?.Mapeamento?.Ativo ?? true;
    }

    public IReadOnlyList<IconOption> Glifos => IconCatalog.Glifos;
    public IReadOnlyList<CorOption> Cores => IconCatalog.Cores;

    public bool IsEdicao => _existing is not null;
    public string Titulo => IsEdicao ? "Editar áudio" : "Novo áudio";

    /// <summary>Solicita que a janela feche (após salvar com sucesso).</summary>
    public event EventHandler? RequestClose;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    private string _nome = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    private string _caminhoArquivo = string.Empty;

    [ObservableProperty]
    private double _volume = 1.0;

    /// <summary>Code-point hex do glifo na barra rápida ("E8D6"). Null = padrão.</summary>
    [ObservableProperty]
    private string? _icone;

    /// <summary>Cor de fundo do botão na barra rápida ("#RRGGBB"). Null = padrão.</summary>
    [ObservableProperty]
    private string? _cor;

    /// <summary>Atalho gravado (canônico) ou vazio.</summary>
    [ObservableProperty]
    private string _teclas = string.Empty;

    /// <summary>Se o atalho está ativo (editável apenas aqui, no editor do áudio).</summary>
    [ObservableProperty]
    private bool _ativo = true;

    /// <summary>Se está no modo "aguardando pressionar tecla".</summary>
    [ObservableProperty]
    private bool _gravandoAtalho;

    [ObservableProperty]
    private string? _erro;

    public string NomeArquivo =>
        string.IsNullOrEmpty(CaminhoArquivo) ? "Nenhum arquivo selecionado" : System.IO.Path.GetFileName(CaminhoArquivo);

    partial void OnCaminhoArquivoChanged(string value)
    {
        OnPropertyChanged(nameof(NomeArquivo));
        // Sugere um nome amigável a partir do arquivo, se ainda vazio.
        if (string.IsNullOrWhiteSpace(Nome) && !string.IsNullOrEmpty(value))
            Nome = System.IO.Path.GetFileNameWithoutExtension(value);
    }

    [RelayCommand]
    private void SelecionarIcone(string? codigo) => Icone = codigo;

    [RelayCommand]
    private void SelecionarCor(string? hex) => Cor = hex;

    [RelayCommand]
    private void PickFile()
    {
        var path = _dialogs.PickAudioFile();
        if (path is not null)
        {
            CaminhoArquivo = path;
            _arquivoAlterado = true;
        }
    }

    private bool PodePreview() =>
        !string.IsNullOrWhiteSpace(CaminhoArquivo) && AudioFileDecoder.IsSupported(CaminhoArquivo);

    [RelayCommand(CanExecute = nameof(PodePreview))]
    private void Preview()
    {
        try
        {
            if (_preview.IsPlaying)
            {
                _preview.Stop();
                return;
            }
            // Preview toca no dispositivo de monitor (fones), não no CABLE.
            // Aplica a mesma curva de volume (VolumeCurve) e o mesmo ganho
            // composto (Volume × SoundboardVolume) usados pelo motor de
            // injeção, para o preview soar como o que de fato chega aos
            // ouvintes.
            var ganhoComposto = VolumeCurve.ToGain((float)Volume)
                * VolumeCurve.ToGain(_settings.Current.SoundboardVolume);
            _preview.Play(CaminhoArquivo, _settings.Current.MonitorDeviceId, ganhoComposto);
        }
        catch (Exception ex)
        {
            Erro = $"Não foi possível reproduzir: {ex.Message}";
        }
    }

    /// <summary>Inicia/cancela a captura de atalho (chamado pelo botão "gravar").</summary>
    [RelayCommand]
    private void ToggleGravacao()
    {
        GravandoAtalho = !GravandoAtalho;
        Erro = null;
    }

    [RelayCommand]
    private void LimparAtalho()
    {
        Teclas = string.Empty;
        GravandoAtalho = false;
    }

    /// <summary>
    /// Recebe a combinação capturada pela view. Retorna false se for só modificadores.
    /// </summary>
    public bool AplicarComboCapturado(KeyCombo? combo)
    {
        if (combo is null)
            return false;
        Teclas = combo.ToString();
        GravandoAtalho = false;
        Erro = null;
        return true;
    }

    private bool PodeSalvar() =>
        !string.IsNullOrWhiteSpace(Nome) && !string.IsNullOrWhiteSpace(CaminhoArquivo);

    /// <summary>Resultado do salvamento (para a janela fechar com sucesso).</summary>
    public bool Salvou { get; private set; }

    [RelayCommand(CanExecute = nameof(PodeSalvar))]
    private async Task Save()
    {
        Erro = null;

        if (!System.IO.File.Exists(CaminhoArquivo))
        {
            Erro = "O arquivo selecionado não existe mais.";
            return;
        }
        if (!AudioFileDecoder.IsSupported(CaminhoArquivo))
        {
            Erro = "Formato não suportado (use .mp3, .wav ou .ogg).";
            return;
        }

        // Valida o atalho antes de tocar no banco.
        KeyCombo? combo = null;
        if (!string.IsNullOrWhiteSpace(Teclas)
            && !KeyComboParser.TryParse(Teclas, out combo, out var erroCombo))
        {
            Erro = erroCombo;
            return;
        }

        _preview.Stop();

        long duracao;
        try
        {
            duracao = AudioFileDecoder.GetDurationMs(CaminhoArquivo);
        }
        catch
        {
            duracao = 0;
        }

        try
        {
            var audio = _existing ?? new Audio();
            var caminhoCacheAnterior = _existing?.CaminhoArquivo;

            audio.Nome = Nome.Trim();
            audio.VolumePadrao = Volume;
            audio.DuracaoMs = duracao;
            audio.Icone = Icone;
            audio.Cor = Cor;

            // Arquivo novo (áudio recém-criado ou "Procurar" usado na edição): importa o
            // conteúdo para o banco e materializa uma cópia própria em cache — a partir
            // daqui, tocar o som não depende mais do arquivo original escolhido pelo usuário.
            if (_existing is null || _arquivoAlterado)
            {
                var (caminhoCache, conteudo, nomeOriginal) = await _fileCache.ImportarAsync(CaminhoArquivo);
                audio.CaminhoArquivo = caminhoCache;
                audio.ArquivoConteudo = conteudo;
                audio.ArquivoNomeOriginal = nomeOriginal;
            }

            if (_existing is null)
                await _audioRepo.AddAsync(audio);
            else
                await _audioRepo.UpdateAsync(audio);

            await SalvarMapeamentoAsync(audio.Id, combo);

            // Só remove o cache antigo depois que o novo já foi salvo com sucesso.
            if (_arquivoAlterado && caminhoCacheAnterior is not null
                && !string.Equals(caminhoCacheAnterior, audio.CaminhoArquivo, StringComparison.OrdinalIgnoreCase))
            {
                _fileCache.RemoverCache(caminhoCacheAnterior);
            }

            Salvou = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (TeclasDuplicadasException)
        {
            Erro = $"O atalho \"{Teclas}\" já está em uso por outro áudio.";
        }
        catch (Exception ex)
        {
            Erro = $"Falha ao salvar: {ex.Message}";
        }
    }

    private async Task SalvarMapeamentoAsync(long audioId, KeyCombo? combo)
    {
        var teclas = combo?.ToString();

        // Sem atalho: remove o mapeamento existente, se houver.
        if (string.IsNullOrEmpty(teclas))
        {
            if (_existingMapeamento is not null)
                await _mapeamentoRepo.DeleteAsync(_existingMapeamento.Id);
            return;
        }

        if (_existingMapeamento is null)
        {
            await _mapeamentoRepo.AddAsync(new Mapeamento
            {
                AudioId = audioId,
                Teclas = teclas,
                Ativo = Ativo,
            });
        }
        else
        {
            _existingMapeamento.Teclas = teclas;
            _existingMapeamento.Ativo = Ativo;
            await _mapeamentoRepo.UpdateAsync(_existingMapeamento);
        }
    }

    /// <summary>Resultado da exclusão (para a janela fechar e a lista recarregar).</summary>
    public bool Excluiu { get; private set; }

    private bool PodeExcluir() => IsEdicao;

    [RelayCommand(CanExecute = nameof(PodeExcluir))]
    private async Task Excluir()
    {
        if (_existing is null) return;
        if (!_dialogs.Confirm("Excluir áudio",
                $"Remover \"{_existing.Nome}\"? O atalho associado também será removido."))
            return;

        _preview.Stop();
        await _audioRepo.DeleteAsync(_existing.Id); // cascade remove o mapeamento
        _fileCache.RemoverCache(_existing.CaminhoArquivo);
        Excluiu = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public void StopPreview() => _preview.Stop();
}
