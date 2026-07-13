using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SoundboardMic.App.Services;
using SoundboardMic.App.ViewModels;

namespace SoundboardMic.App.Views;

public partial class AudioEditDialog : Window
{
    private readonly AudioEditViewModel _vm;

    public AudioEditDialog(AudioEditViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;

        _vm.RequestClose += (_, _) => { DialogResult = true; Close(); };

        // Captura de atalho enquanto a janela está em modo de gravação.
        PreviewKeyDown += OnPreviewKeyDown;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_vm.GravandoAtalho)
            return;

        // Esc cancela a gravação.
        if (e.Key == Key.Escape)
        {
            _vm.ToggleGravacaoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var combo = HotkeyCapture.FromKeyEvent(e);
        if (combo is not null)
        {
            _vm.AplicarComboCapturado(combo);
            e.Handled = true;
        }
    }

    private void Fechar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _vm.Salvou;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _vm.StopPreview();
        base.OnClosing(e);
    }
}
