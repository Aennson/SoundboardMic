using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SoundboardMic.App.Services;
using SoundboardMic.App.ViewModels;

namespace SoundboardMic.App.Views;

/// <summary>
/// Barra rápida flutuante: topmost, sem borda e sem ativação — o clique dispara
/// o som sem roubar o foco da janela ativa (jogo/chamada). Nunca é fechada fora
/// do shutdown, apenas ocultada.
/// </summary>
public partial class QuickBarWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExToolWindow = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern long GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern long SetWindowLongPtrW(IntPtr hWnd, int nIndex, long dwNewLong);

    private readonly QuickBarViewModel _vm;
    private readonly ISettingsService _settings;

    public QuickBarWindow(QuickBarViewModel viewModel, ISettingsService settings)
    {
        _vm = viewModel;
        _settings = settings;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoadedOnce;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // NOACTIVATE: cliques não tiram o foco do app ativo.
        // TOOLWINDOW: fora do Alt-Tab.
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtrW(hwnd, GwlExStyle);
        SetWindowLongPtrW(hwnd, GwlExStyle, style | WsExNoActivate | WsExToolWindow);

        AjustarTamanhoDosBotoes();
    }

    private void OnLoadedOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedOnce;
        PosicionarInicial();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Em Application.Shutdown o Cancel é ignorado pelo WPF; fora dele,
        // a barra apenas se esconde (janela fechada não reabre).
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        DragMove(); // bloqueia até soltar

        var (left, top) = QuickBarMetrics.ClampPosicao(
            Left, Top, ActualWidth, ActualHeight,
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Left = left;
        Top = top;

        _vm.SalvarPosicao(Left, Top);
        Topmost = true; // reafirma o z-order acima da taskbar
    }

    private void PosicionarInicial()
    {
        var s = _settings.Current;
        var work = SystemParameters.WorkArea;

        var left = s.QuickBarLeft ?? work.Left + 8;
        var top = s.QuickBarTop ?? work.Bottom - ActualHeight - 8;

        (Left, Top) = QuickBarMetrics.ClampPosicao(
            left, top, ActualWidth, ActualHeight,
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
    }

    private void AjustarTamanhoDosBotoes()
    {
        // Na horizontal a barra inteira precisa caber na altura da taskbar;
        // o mesmo tamanho é usado na vertical, por consistência visual.
        var alturaTaskbar = QuickBarMetrics.AlturaTaskbar(
            SystemParameters.PrimaryScreenHeight, SystemParameters.WorkArea.Bottom);
        _vm.TamanhoBotao = QuickBarMetrics.TamanhoBotao(alturaTaskbar, padding: 4);
    }
}
