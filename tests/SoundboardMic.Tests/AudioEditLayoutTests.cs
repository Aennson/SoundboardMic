using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;

namespace SoundboardMic.Tests;

[Collection("WPF")]
public class AudioEditLayoutTests
{
    [Fact]
    public void BothTabs_FitAllControlsWithoutScrolling()
    {
        WpfTestHost.Run(() =>
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SoundboardMic.sln")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            var document = XDocument.Load(Path.Combine(directory.FullName,
                "src", "SoundboardMic.App", "Views", "AudioEditDialog.xaml"));
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            document.Root!.Attribute(x + "Class")!.Remove();
            document.Root.SetAttributeValue("FontFamily", "Segoe UI Variable, Segoe UI");
            foreach (var click in document.Descendants().Attributes("Click").ToList())
                click.Remove();

            var window = (Window)XamlReader.Parse(document.ToString());
            window.DataContext = new EditorData();
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(window.Width, window.Height));
            content.Arrange(new Rect(0, 0, window.Width, window.Height));
            content.UpdateLayout();

            var tab = Descendants<TabControl>(content).Single();
            var input = Descendants<TextBox>(tab).Single(t =>
                BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path.Path == "CategoriaTexto");
            var suggestions = Descendants<ItemsControl>(tab).Single(t =>
                BindingOperations.GetBinding(t, ItemsControl.ItemsSourceProperty)?.Path.Path == "CategoriasDisponiveis");
            Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName == "ScrollViewer");
            var lastSuggestion = Descendants<Button>(suggestions).Last();
            AssertInside(tab, lastSuggestion);
            AssertInside(tab, input);
            Assert.True(input.IsEnabled);

            foreach (var index in new[] { 0, 1 })
            {
                tab.SelectedIndex = index;
                content.UpdateLayout();
                foreach (var control in Descendants<Control>(tab).Where(c =>
                    c is Button or TextBox or Slider or System.Windows.Controls.Primitives.ToggleButton))
                    AssertInside(tab, control);
            }
        });
    }

    private sealed class EditorData
    {
        public string CategoriaTexto { get; set; } = "";
        public string[] CategoriasDisponiveis { get; } =
            Enumerable.Range(1, 20).Select(i => $"Categoria {i}").ToArray();
        public string Nome { get; set; } = "Som de teste";
        public string Cor { get; set; } = "#008080";
        public string Icone { get; set; } = "E8D6";
        public string Erro { get; set; } = "Não foi possível salvar. Verifique os dados e tente novamente.";
        public IReadOnlyList<SoundboardMic.App.Services.IconOption> Glifos =>
            SoundboardMic.App.Services.IconCatalog.Glifos;
        public IReadOnlyList<SoundboardMic.App.Services.CorOption> Cores =>
            SoundboardMic.App.Services.IconCatalog.Cores;
    }

    private static void AssertInside(FrameworkElement parent, FrameworkElement child)
    {
        var bounds = child.TransformToAncestor(parent).TransformBounds(new Rect(child.RenderSize));
        Assert.True(bounds.Top >= 0 && bounds.Bottom <= parent.ActualHeight &&
            bounds.Left >= 0 && bounds.Right <= parent.ActualWidth,
            $"{child.GetType().Name} bounds {bounds} exceed editor height {parent.ActualHeight}.");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
