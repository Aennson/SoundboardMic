using System.Windows.Controls;
using System.Windows.Input;

namespace SoundboardMic.App.Views;

public partial class EditorDeSonsView : UserControl
{
    public EditorDeSonsView()
    {
        InitializeComponent();
        // Enter aplica o tempo digitado sem precisar sair do campo.
        AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key == Key.Enter && e.OriginalSource is TextBox caixa)
                caixa.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }), true);
    }
}
