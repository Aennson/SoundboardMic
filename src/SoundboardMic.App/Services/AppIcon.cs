using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace SoundboardMic.App;

/// <summary>
/// Gera o ícone do app em runtime (quadrado arredondado com gradiente violeta e um
/// microfone branco), evitando depender de um arquivo .ico externo. Serve tanto para
/// a bandeja (System.Drawing.Icon) quanto para a janela (ImageSource).
/// </summary>
public static class AppIcon
{
    private static Drawing.Icon? _icon;
    private static System.Windows.Media.ImageSource? _imageSource;

    public static Drawing.Icon Get() => _icon ??= CreateIcon();

    public static System.Windows.Media.ImageSource GetImageSource() =>
        _imageSource ??= CreateImageSource();

    private static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var rect = new Rectangle(0, 0, size, size);
        var radius = size * 0.28f;

        // Fundo arredondado com gradiente.
        using (var path = RoundedRect(rect, radius))
        using (var brush = new LinearGradientBrush(rect,
                   Drawing.Color.FromArgb(0x8B, 0x6B, 0xFF),
                   Drawing.Color.FromArgb(0x5B, 0x8D, 0xEF), 45f))
        {
            g.FillPath(brush, path);
        }

        // Microfone estilizado (cápsula + haste + base).
        using var white = new SolidBrush(Drawing.Color.White);
        using var pen = new Pen(Drawing.Color.White, size * 0.06f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        float cx = size / 2f;
        float capW = size * 0.26f;
        float capH = size * 0.40f;
        float capTop = size * 0.16f;
        var capRect = new RectangleF(cx - capW / 2f, capTop, capW, capH);
        g.FillPath(white, RoundedRectF(capRect, capW / 2f));

        // Arco do suporte.
        float arcSize = size * 0.44f;
        var arcRect = new RectangleF(cx - arcSize / 2f, size * 0.30f, arcSize, arcSize);
        g.DrawArc(pen, arcRect, 20, 140);

        // Haste + base.
        float stemTop = arcRect.Bottom - size * 0.06f;
        g.DrawLine(pen, cx, stemTop, cx, size * 0.84f);
        g.DrawLine(pen, cx - size * 0.12f, size * 0.84f, cx + size * 0.12f, size * 0.84f);

        return bmp;
    }

    private static GraphicsPath RoundedRect(Rectangle r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath RoundedRectF(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Drawing.Icon CreateIcon()
    {
        using var bmp = Render(64);
        var hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Drawing.Icon.FromHandle(hIcon);
            return (Drawing.Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static System.Windows.Media.ImageSource CreateImageSource()
    {
        using var bmp = Render(128);
        var hBitmap = bmp.GetHbitmap();
        try
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
