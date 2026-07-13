using SoundboardMic.App.ViewModels;

namespace SoundboardMic.App.Services;

/// <summary>Interações com janelas/diálogos, mantendo os ViewModels livres de WPF.</summary>
public interface IDialogService
{
    /// <summary>Abre um seletor de arquivos de áudio. Retorna o caminho ou null.</summary>
    string? PickAudioFile();

    /// <summary>Confirmação sim/não.</summary>
    bool Confirm(string titulo, string mensagem);

    /// <summary>Aviso simples.</summary>
    void Info(string titulo, string mensagem);

    /// <summary>Abre o editor de áudio (novo ou edição). Retorna true se salvo.</summary>
    bool ShowAudioEditor(AudioEditViewModel viewModel);
}
