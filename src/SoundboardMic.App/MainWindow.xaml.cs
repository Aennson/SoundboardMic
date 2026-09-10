using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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

        // Sem essa correção, uma janela "chromeless" (WindowStyle=None +
        // AllowsTransparency + WindowChrome) se estende além da área útil da
        // tela ao maximizar (por cima da barra de tarefas/bordas do monitor),
        // cortando conteúdo perto das bordas (ex.: o card de status no rodapé
        // da sidebar). O hook do WM_GETMINMAXINFO faz o Windows respeitar a
        // área de trabalho real do monitor ao maximizar.
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Cantos arredondados ficam estranhos colados na borda da tela quando
        // maximizada; nesse estado a janela já ocupa a área útil inteira.
        RootBorder.CornerRadius = WindowState == WindowState.Maximized
            ? new CornerRadius(0)
            : new CornerRadius(12);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        // Área do monitor atual (considera múltiplos monitores corretamente).
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref monitorInfo);
            var workArea = monitorInfo.rcWork;
            var monitorArea = monitorInfo.rcMonitor;

            mmi.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
            mmi.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
            mmi.ptMaxSize.X = Math.Abs(workArea.Right - workArea.Left);
            mmi.ptMaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private const int MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
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

    public void RestoreFromTray()
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
