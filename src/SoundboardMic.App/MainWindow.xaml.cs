using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SoundboardMic.App.Services;
using SoundboardMic.App.ViewModels;

namespace SoundboardMic.App;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settings;
    private bool _reallyExit;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;

        Tray.Icon = AppIcon.Get();
        Icon = AppIcon.GetImageSource();

        // Captura de teclas para a gravação da tecla de pânico nas configurações.
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var settingsVm = ViewModel.Settings;
        if (!settingsVm.GravandoPanico)
            return;

        if (e.Key == Key.Escape)
        {
            settingsVm.ToggleGravacaoPanicoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var combo = HotkeyCapture.FromKeyEvent(e);
        if (combo is not null)
        {
            await settingsVm.AplicarPanicoCapturado(combo);
            e.Handled = true;
        }
    }

    // ---- Barra de título custom ----
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleMaximize();
        else if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ---- Bandeja ----
    private void Tray_DoubleClick(object sender, RoutedEventArgs e) => RestoreFromTray();
    private void TrayOpen_Click(object sender, RoutedEventArgs e) => RestoreFromTray();

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _reallyExit = true;
        Application.Current.Shutdown();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Minimizar para a bandeja em vez de fechar, se configurado.
        if (!_reallyExit && _settings.Current.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            Tray.ShowBalloonTip("SoundboardMic", "Continua rodando na bandeja. As hotkeys seguem ativas.",
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
            return;
        }
        Tray.Dispose();
        base.OnClosing(e);
    }
}
