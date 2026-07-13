using System.Windows;
using Microsoft.Win32;
using SoundboardMic.App.ViewModels;
using SoundboardMic.App.Views;

namespace SoundboardMic.App.Services;

public class DialogService : IDialogService
{
    public string? PickAudioFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Selecionar arquivo de áudio",
            Filter = "Áudio (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public bool Confirm(string titulo, string mensagem) =>
        MessageDialog.Confirm(Application.Current.MainWindow, titulo, mensagem);

    public void Info(string titulo, string mensagem) =>
        MessageDialog.Info(Application.Current.MainWindow, titulo, mensagem);

    public bool ShowAudioEditor(AudioEditViewModel viewModel)
    {
        var dlg = new AudioEditDialog(viewModel) { Owner = Application.Current.MainWindow };
        return dlg.ShowDialog() == true;
    }
}
