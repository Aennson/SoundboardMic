using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SoundboardMic.App.Views;

public partial class MessageDialog : Window
{
    // Glifos do Segoe Fluent Icons.
    private const int GlyphWarning = 0xE7BA;
    private const int GlyphInfo = 0xE946;

    private MessageDialog()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    /// <summary>Diálogo de confirmação (Confirmar / Cancelar).</summary>
    public static bool Confirm(Window? owner, string titulo, string mensagem)
    {
        var dlg = Create(owner, titulo, mensagem, GlyphWarning, "#FFB454");
        return dlg.ShowDialog() == true;
    }

    /// <summary>Diálogo informativo (apenas OK).</summary>
    public static void Info(Window? owner, string titulo, string mensagem)
    {
        var dlg = Create(owner, titulo, mensagem, GlyphInfo, "#5B8DEF");
        dlg.BtnCancel.Visibility = Visibility.Collapsed;
        dlg.BtnOk.Content = "OK";
        dlg.ShowDialog();
    }

    private static MessageDialog Create(Window? owner, string titulo, string mensagem, int glyph, string cor)
    {
        var dlg = new MessageDialog
        {
            Owner = owner ?? Application.Current.MainWindow,
        };
        dlg.TitleText.Text = titulo;
        dlg.MessageText.Text = mensagem;
        dlg.IconGlyph.Text = char.ConvertFromUtf32(glyph);
        dlg.IconGlyph.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cor));
        return dlg;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
