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

    private const uint EventSystemForeground = 0x0003;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    private delegate void WinEventProc(
        IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern long GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern long SetWindowLongPtrW(IntPtr hWnd, int nIndex, long dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr module, WinEventProc callback,
        uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    private readonly QuickBarViewModel _vm;
    private readonly ISettingsService _settings;

    // Mantém o delegate vivo enquanto o hook existir (senão o GC o coleta).
    private WinEventProc? _foregroundCallback;
    private IntPtr _foregroundHook;

    public QuickBarWindow(QuickBarViewModel viewModel, ISettingsService settings)
    {
        _vm = viewModel;
        _settings = settings;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoadedOnce;
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is true)
                ReassertTopmost();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // NOACTIVATE: cliques não tiram o foco do app ativo.
        // TOOLWINDOW: fora do Alt-Tab.
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtrW(hwnd, GwlExStyle);
        SetWindowLongPtrW(hwnd, GwlExStyle, style | WsExNoActivate | WsExToolWindow);

        // A taskbar também é topmost e o shell a reergue quando o usuário troca
        // de aplicativo; como esta janela é NOACTIVATE (nunca é reerguida por
        // ativação), reafirma o topo da camada topmost a cada troca de foreground
        // para não ficar por baixo da taskbar.
        _foregroundCallback = OnForegroundChanged;
        _foregroundHook = SetWinEventHook(
            EventSystemForeground, EventSystemForeground, IntPtr.Zero,
            _foregroundCallback, 0, 0, 0 /* WINEVENT_OUTOFCONTEXT */);

        AjustarTamanhoDosBotoes();
    }

    private void OnForegroundChanged(
        IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (IsVisible)
            ReassertTopmost();
    }

    private void ReassertTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }
        base.OnClosed(e);
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
        ReassertTopmost();
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
