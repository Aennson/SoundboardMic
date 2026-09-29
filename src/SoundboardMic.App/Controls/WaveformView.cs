using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;

namespace SoundboardMic.App.Controls;

/// <summary>
/// Forma de onda com seleção de trecho. Mostra os picos do áudio, escurece o que fica fora de
/// [<see cref="Inicio"/>, <see cref="Fim"/>] e deixa o usuário mover as alças com o mouse
/// (ou arrastar uma seleção nova) e com o teclado (setas = 10 ms, Shift+setas = 100 ms,
/// Tab alterna a alça ativa).
/// </summary>
public class WaveformView : FrameworkElement
{
    private const double LarguraAlca = 10;
    private const double AlturaRegua = 18;
    private static readonly TimeSpan PassoFino = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan PassoGrosso = TimeSpan.FromMilliseconds(100);

    private enum Arrasto { Nenhum, Inicio, Fim, Nova }

    private Arrasto _arrasto;
    private TimeSpan _ancora;
    private bool _alcaFimAtiva;

    static WaveformView()
    {
        FocusableProperty.OverrideMetadata(typeof(WaveformView), new FrameworkPropertyMetadata(true));
    }

    public WaveformView()
    {
        AutomationProperties.SetName(this, "Forma de onda do áudio");
        AutomationProperties.SetHelpText(this,
            "Arraste para selecionar o trecho. Setas movem a alça ativa em 10 ms, Shift+setas em 100 ms, Tab troca a alça.");
        SnapsToDevicePixels = true;
        Cursor = Cursors.IBeam;
    }

    public static readonly DependencyProperty PicosProperty = DependencyProperty.Register(
        nameof(Picos), typeof(float[]), typeof(WaveformView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Pico absoluto (0..1) por coluna, do início ao fim do áudio.</summary>
    public float[]? Picos
    {
        get => (float[]?)GetValue(PicosProperty);
        set => SetValue(PicosProperty, value);
    }

    public static readonly DependencyProperty DuracaoProperty = DependencyProperty.Register(
        nameof(Duracao), typeof(TimeSpan), typeof(WaveformView),
        new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.AffectsRender));

    public TimeSpan Duracao
    {
        get => (TimeSpan)GetValue(DuracaoProperty);
        set => SetValue(DuracaoProperty, value);
    }

    public static readonly DependencyProperty InicioProperty = DependencyProperty.Register(
        nameof(Inicio), typeof(TimeSpan), typeof(WaveformView),
        new FrameworkPropertyMetadata(TimeSpan.Zero,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public TimeSpan Inicio
    {
        get => (TimeSpan)GetValue(InicioProperty);
        set => SetValue(InicioProperty, value);
    }

    public static readonly DependencyProperty FimProperty = DependencyProperty.Register(
        nameof(Fim), typeof(TimeSpan), typeof(WaveformView),
        new FrameworkPropertyMetadata(TimeSpan.Zero,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public TimeSpan Fim
    {
        get => (TimeSpan)GetValue(FimProperty);
        set => SetValue(FimProperty, value);
    }

    public static readonly DependencyProperty CursorPosicaoProperty = DependencyProperty.Register(
        nameof(CursorPosicao), typeof(TimeSpan?), typeof(WaveformView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Posição da pré-escuta; null esconde o cursor.</summary>
    public TimeSpan? CursorPosicao
    {
        get => (TimeSpan?)GetValue(CursorPosicaoProperty);
        set => SetValue(CursorPosicaoProperty, value);
    }

    public static readonly DependencyProperty OndaBrushProperty = DependencyProperty.Register(
        nameof(OndaBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.MediumPurple, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush OndaBrush
    {
        get => (Brush)GetValue(OndaBrushProperty);
        set => SetValue(OndaBrushProperty, value);
    }

    public static readonly DependencyProperty TextoBrushProperty = DependencyProperty.Register(
        nameof(TextoBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush TextoBrush
    {
        get => (Brush)GetValue(TextoBrushProperty);
        set => SetValue(TextoBrushProperty, value);
    }

    private Rect AreaOnda => new(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight - AlturaRegua));

    // ---- Conversões tempo <-> x ----

    internal double XDe(TimeSpan t)
    {
        var total = Duracao.TotalMilliseconds;
        if (total <= 0 || ActualWidth <= 0) return 0;
        return Math.Clamp(t.TotalMilliseconds / total, 0, 1) * ActualWidth;
    }

    internal TimeSpan TempoDe(double x)
    {
        if (ActualWidth <= 0) return TimeSpan.Zero;
        var fracao = Math.Clamp(x / ActualWidth, 0, 1);
        return TimeSpan.FromMilliseconds(Math.Round(fracao * Duracao.TotalMilliseconds));
    }

    // ---- Render ----

    protected override void OnRender(DrawingContext dc)
    {
        var area = AreaOnda;
        // Fundo transparente para receber o mouse em toda a área.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (area.Width <= 0 || area.Height <= 0) return;

        var meio = area.Height / 2;
        var picos = Picos;
        if (picos is { Length: > 0 })
        {
            var colunas = (int)Math.Max(1, Math.Floor(area.Width / 3));
            var amostra = Reamostrar(picos, colunas);
            var passo = area.Width / amostra.Length;
            var largura = Math.Max(1, passo - 1);
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                for (var i = 0; i < amostra.Length; i++)
                {
                    var h = Math.Max(1, amostra[i] * (area.Height - 4));
                    var x = i * passo;
                    var y = meio - h / 2;
                    ctx.BeginFigure(new Point(x, y), true, true);
                    ctx.LineTo(new Point(x + largura, y), false, false);
                    ctx.LineTo(new Point(x + largura, y + h), false, false);
                    ctx.LineTo(new Point(x, y + h), false, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(OndaBrush, null, geo);
        }
        else
        {
            dc.DrawLine(new Pen(OndaBrush, 1), new Point(0, meio), new Point(area.Width, meio));
        }

        if (Duracao > TimeSpan.Zero)
        {
            var xi = XDe(Inicio);
            var xf = XDe(Fim);
            var sombra = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
            sombra.Freeze();
            if (xi > 0) dc.DrawRectangle(sombra, null, new Rect(0, 0, xi, area.Height));
            if (xf < area.Width) dc.DrawRectangle(sombra, null, new Rect(xf, 0, area.Width - xf, area.Height));

            DesenharAlca(dc, xi, area.Height, !_alcaFimAtiva && IsKeyboardFocused);
            DesenharAlca(dc, xf, area.Height, _alcaFimAtiva && IsKeyboardFocused);

            if (CursorPosicao is { } cursor)
            {
                var xc = XDe(cursor);
                dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(xc, 0), new Point(xc, area.Height));
            }

            DesenharRegua(dc, area);
        }

        if (IsKeyboardFocused)
        {
            var foco = new Pen(TryFindResource("FocusBrush") as Brush ?? Brushes.White, 1) { DashStyle = DashStyles.Dash };
            dc.DrawRectangle(null, foco, new Rect(0.5, 0.5, Math.Max(0, area.Width - 1), Math.Max(0, area.Height - 1)));
        }
    }

    private void DesenharAlca(DrawingContext dc, double x, double altura, bool ativa)
    {
        dc.DrawLine(new Pen(OndaBrush, ativa ? 3 : 2), new Point(x, 0), new Point(x, altura));
        var alca = new Rect(x - LarguraAlca / 2, 0, LarguraAlca, 14);
        dc.DrawRoundedRectangle(OndaBrush, ativa ? new Pen(Brushes.White, 1.5) : null, alca, 3, 3);
    }

    private void DesenharRegua(DrawingContext dc, Rect area)
    {
        var total = Duracao.TotalSeconds;
        if (total <= 0) return;
        var passos = new[] { 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };
        var maxMarcas = Math.Max(2, area.Width / 80);
        var passo = passos.FirstOrDefault(p => total / p <= maxMarcas, 1200);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var tipo = new Typeface("Segoe UI");
        var pen = new Pen(TextoBrush, 1);
        for (var s = 0.0; s <= total + 1e-6; s += passo)
        {
            var x = s / total * area.Width;
            dc.DrawLine(pen, new Point(x, area.Height), new Point(x, area.Height + 4));
            var texto = new FormattedText(FormatarRegua(s, passo), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, tipo, 10, TextoBrush, dpi);
            var tx = Math.Clamp(x - texto.Width / 2, 0, Math.Max(0, area.Width - texto.Width));
            dc.DrawText(texto, new Point(tx, area.Height + 4));
        }
    }

    private static string FormatarRegua(double segundos, double passo)
    {
        var t = TimeSpan.FromSeconds(segundos);
        return passo < 1
            ? $"{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds / 100}"
            : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    private static float[] Reamostrar(float[] picos, int largura)
    {
        if (picos.Length <= largura) return picos;
        var saida = new float[largura];
        for (var i = 0; i < largura; i++)
        {
            var a = (int)((long)i * picos.Length / largura);
            var b = (int)Math.Max(a + 1, (long)(i + 1) * picos.Length / largura);
            var max = 0f;
            for (var j = a; j < b; j++) if (picos[j] > max) max = picos[j];
            saida[i] = max;
        }
        return saida;
    }

    // ---- Mouse ----

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Duracao <= TimeSpan.Zero) return;
        Focus();
        var x = e.GetPosition(this).X;
        var di = Math.Abs(x - XDe(Inicio));
        var df = Math.Abs(x - XDe(Fim));
        if (di <= LarguraAlca && di <= df)
        {
            _arrasto = Arrasto.Inicio;
            _alcaFimAtiva = false;
        }
        else if (df <= LarguraAlca)
        {
            _arrasto = Arrasto.Fim;
            _alcaFimAtiva = true;
        }
        else
        {
            _arrasto = Arrasto.Nova;
            _ancora = TempoDe(x);
        }
        CaptureMouse();
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var x = e.GetPosition(this).X;
        if (_arrasto == Arrasto.Nenhum)
        {
            var perto = Duracao > TimeSpan.Zero &&
                        (Math.Abs(x - XDe(Inicio)) <= LarguraAlca || Math.Abs(x - XDe(Fim)) <= LarguraAlca);
            Cursor = perto ? Cursors.SizeWE : Cursors.IBeam;
            return;
        }
        ArrastarPara(TempoDe(x));
    }

    /// <summary>Aplica o arrasto em andamento até o tempo <paramref name="t"/>.</summary>
    private void ArrastarPara(TimeSpan t)
    {
        switch (_arrasto)
        {
            case Arrasto.Inicio:
                Inicio = t < Fim ? t : Fim;
                break;
            case Arrasto.Fim:
                Fim = t > Inicio ? t : Inicio;
                break;
            case Arrasto.Nova:
                // Ordem importa: com fim antes do novo início o VM empurraria o início.
                var (a, b) = t < _ancora ? (t, _ancora) : (_ancora, t);
                if (a >= Fim) { Fim = b; Inicio = a; }
                else { Inicio = a; Fim = b; }
                _alcaFimAtiva = t >= _ancora;
                break;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_arrasto == Arrasto.Nenhum) return;
        _arrasto = Arrasto.Nenhum;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _arrasto = Arrasto.Nenhum;
    }

    // ---- Teclado ----

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Duracao <= TimeSpan.Zero) return;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var passo = shift ? PassoGrosso : PassoFino;
        switch (e.Key)
        {
            case Key.Left:
                MoverAlcaAtiva(-passo);
                break;
            case Key.Right:
                MoverAlcaAtiva(passo);
                break;
            case Key.Home:
                MoverAlcaAtiva(-Duracao);
                break;
            case Key.End:
                MoverAlcaAtiva(Duracao);
                break;
            // Tab: alça início → fim; Shift+Tab: fim → início. Nas pontas o foco sai normalmente.
            case Key.Tab when !shift && !_alcaFimAtiva:
            case Key.Tab when shift && _alcaFimAtiva:
                AlcaFimAtiva = !_alcaFimAtiva;
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>Move a alça ativa, sem cruzar a outra nem sair do áudio.</summary>
    internal void MoverAlcaAtiva(TimeSpan delta)
    {
        if (_alcaFimAtiva)
        {
            var t = Fim + delta;
            Fim = t < Inicio ? Inicio : t > Duracao ? Duracao : t;
        }
        else
        {
            var t = Inicio + delta;
            Inicio = t < TimeSpan.Zero ? TimeSpan.Zero : t > Fim ? Fim : t;
        }
    }

    internal bool AlcaFimAtiva
    {
        get => _alcaFimAtiva;
        set { _alcaFimAtiva = value; InvalidateVisual(); }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        InvalidateVisual();
    }
}
