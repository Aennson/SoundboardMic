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

    private readonly Audio? _existing;
    private readonly Mapeamento? _existingMapeamento;

    public AudioEditViewModel(
        IAudioRepository audioRepo,
        IMapeamentoRepository mapeamentoRepo,
        IDialogService dialogs,
        IPlaybackService preview,
        ISettingsService settings,
        AudioItemViewModel? editing)
    {
        _audioRepo = audioRepo;
        _mapeamentoRepo = mapeamentoRepo;
        _dialogs = dialogs;
        _preview = preview;
        _settings = settings;

        _existing = editing?.Audio;
        _existingMapeamento = editing?.Mapeamento;

        if (editing is not null)
        {
            _nome = editing.Audio.Nome;
            _caminhoArquivo = editing.Audio.CaminhoArquivo;
            _volume = editing.Audio.VolumePadrao;
            _teclas = editing.Mapeamento?.Teclas ?? string.Empty;
        }
    }

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

    /// <summary>Atalho gravado (canônico) ou vazio.</summary>
    [ObservableProperty]
    private string _teclas = string.Empty;

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
    private void PickFile()
    {
        var path = _dialogs.PickAudioFile();
        if (path is not null)
            CaminhoArquivo = path;
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
            _preview.Play(CaminhoArquivo, _settings.Current.MonitorDeviceId, (float)Volume);
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
            audio.Nome = Nome.Trim();
            audio.CaminhoArquivo = CaminhoArquivo;
            audio.VolumePadrao = Volume;
            audio.DuracaoMs = duracao;

            if (_existing is null)
                await _audioRepo.AddAsync(audio);
            else
                await _audioRepo.UpdateAsync(audio);

            await SalvarMapeamentoAsync(audio.Id, combo);

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
                Ativo = true,
            });
        }
        else
        {
            _existingMapeamento.Teclas = teclas;
            await _mapeamentoRepo.UpdateAsync(_existingMapeamento);
        }
    }

    public void StopPreview() => _preview.Stop();
}
