using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.Models;

namespace SoundboardMic.Tests;

[Collection("WPF")]
public class SoundCardLayoutTests
{
    [Fact]
    public void AbrirTela_SecoesECardsEntramEscalonadosETerminamVisiveis()
    {
        WpfTestHost.Run(() =>
        {
            var view = CarregarView();
            var memes = new CategoriaGroupViewModel(1, "Memes", 0);
            memes.Itens.Add(new AudioItemViewModel(new Audio { Id = 1, Nome = "a" }, null));
            var jogos = new CategoriaGroupViewModel(2, "Jogos", 1);
            jogos.Itens.Add(new AudioItemViewModel(new Audio { Id = 2, Nome = "b" }, null));
            var aquecimento = new CategoriaGroupViewModel(9, "Aquecimento", 0);
            aquecimento.Itens.Add(new AudioItemViewModel(new Audio { Id = 9, Nome = "z" }, null));
            view.DataContext = new { Grupos = new[] { aquecimento }, ListaVazia = false };
            var window = CriarJanela(view);
            window.Show();
            try
            {
                // Primeira renderização (JIT/templates) pode travar o dispatcher; só medimos a segunda.
                view.UpdateLayout();
                Pump(TimeSpan.FromMilliseconds(700));
                view.DataContext = new { Grupos = new[] { memes, jogos }, ListaVazia = false };
                view.UpdateLayout();
                var g1 = Descendants<ContentPresenter>(view).First(p => p.Content == memes);
                var g2 = Descendants<ContentPresenter>(view).First(p => p.Content == jogos);
                var card1 = Descendants<ContentPresenter>(view).First(p => p.Content == memes.Itens[0]);
                var card2 = Descendants<ContentPresenter>(view).First(p => p.Content == jogos.Itens[0]);
                if (SystemParameters.ClientAreaAnimation)
                {
                    // Momento em que cada elemento passa de metade visível: seção 1 < card 1, seção 1 < seção 2 < card 2.
                    var marcas = new DateTime?[4];
                    var inicioTeste = DateTime.UtcNow;
                    var elementos = new FrameworkElement[] { g1, card1, g2, card2 };
                    var fim = DateTime.UtcNow.AddMilliseconds(1200);
                    while (DateTime.UtcNow < fim && marcas.Any(m => m is null))
                    {
                        Pump(TimeSpan.FromMilliseconds(5));
                        for (var i = 0; i < 4; i++)
                            if (marcas[i] is null && elementos[i].Opacity > 0.5) marcas[i] = DateTime.UtcNow;
                    }
                    Assert.All(marcas, m => Assert.NotNull(m));
                    Assert.True(marcas[2] - marcas[0] >= TimeSpan.FromMilliseconds(60),
                        $"seções deveriam entrar em sequência: {string.Join(" | ", marcas.Select(m => (m - inicioTeste)?.TotalMilliseconds))}");
                    Assert.True(marcas[1] > marcas[0] && marcas[3] > marcas[2], "cards entram após a própria seção");
                }

                Pump(TimeSpan.FromMilliseconds(900));
                foreach (var el in new[] { g1, g2, card2 })
                    Assert.Equal(1, el.Opacity, 3);
                Assert.Equal(1, ((ScaleTransform)g2.LayoutTransform).ScaleY, 3);
                Assert.Equal(1, ((ScaleTransform)card2.RenderTransform).ScaleX, 3);

                // Trocar de aba esconde e reexibe a tela: a entrada deve rodar de novo.
                if (SystemParameters.ClientAreaAnimation)
                {
                    view.Visibility = Visibility.Collapsed;
                    Pump(TimeSpan.FromMilliseconds(80));
                    view.Visibility = Visibility.Visible;
                    Pump(TimeSpan.FromMilliseconds(60));
                    Assert.True(g2.Opacity < 0.9 || card2.Opacity < 0.9,
                        $"reexibir deveria reanimar: g2={g2.Opacity} card2={card2.Opacity}");
                    Pump(TimeSpan.FromMilliseconds(1000));
                    Assert.Equal(1, g2.Opacity, 3);
                    Assert.Equal(1, card2.Opacity, 3);
                    Assert.Equal(1, ((ScaleTransform)g2.LayoutTransform).ScaleY, 3);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ExcluirCard_AnimaSaidaDoCardEDaSecaoSemErros()
    {
        WpfTestHost.Run(() =>
        {
            var view = CarregarView();
            var a = new AudioItemViewModel(new Audio { Id = 1, Nome = "a" }, null);
            var b = new AudioItemViewModel(new Audio { Id = 2, Nome = "b" }, null);
            var memes = new CategoriaGroupViewModel(1, "Memes", 0);
            memes.Itens.Add(a);
            memes.Itens.Add(b);
            view.DataContext = new { Grupos = new[] { memes }, ListaVazia = false };
            var window = CriarJanela(view);
            window.Show();
            try
            {
                view.UpdateLayout();
                Pump(TimeSpan.FromMilliseconds(700)); // conclui a animação de entrada
                var cardContainer = Descendants<ContentPresenter>(view).First(p => p.Content == a);
                var grupoContainer = Descendants<ContentPresenter>(view).First(p => p.Content == memes);

                a.Removendo = true;
                memes.Removendo = true;
                Pump(TimeSpan.FromMilliseconds(450));

                var scale = (ScaleTransform)cardContainer.LayoutTransform;
                var cardBorder = Descendants<Border>(cardContainer).First();
                Assert.True(cardBorder.Opacity < 0.01 && scale.ScaleX < 0.01 &&
                    ((ScaleTransform)grupoContainer.LayoutTransform).ScaleY < 0.01,
                    $"opacity={cardBorder.Opacity} scaleX={scale.ScaleX} grupoY={((ScaleTransform)grupoContainer.LayoutTransform).ScaleY} grupoOp={grupoContainer.Opacity} hit={cardContainer.IsHitTestVisible}");
                Assert.False(cardContainer.IsHitTestVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SalvarEdicao_ReanimaSomenteOCardAlterado()
    {
        WpfTestHost.Run(() =>
        {
            var view = CarregarView();
            var a = new AudioItemViewModel(new Audio { Id = 1, Nome = "a" }, null);
            var b = new AudioItemViewModel(new Audio { Id = 2, Nome = "b" }, null);
            var memes = new CategoriaGroupViewModel(1, "Memes", 0);
            memes.Itens.Add(a);
            memes.Itens.Add(b);
            var grupos = new ObservableCollection<CategoriaGroupViewModel> { memes };
            view.DataContext = new { Grupos = grupos, ListaVazia = false };
            var window = CriarJanela(view);
            window.Show();
            try
            {
                view.UpdateLayout();
                Pump(TimeSpan.FromMilliseconds(900)); // conclui a entrada inicial

                var containerA = Descendants<ContentPresenter>(view).First(p => p.Content == a);
                var containerB = Descendants<ContentPresenter>(view).First(p => p.Content == b);
                var grupoContainer = Descendants<ContentPresenter>(view).First(p => p.Content == memes);

                // Salvar recarrega a grade; os containers precisam sobreviver à sincronização.
                a.Atualizar(new Audio { Id = 1, Nome = "a editado" }, null);
                GradeSync.SincronizarGrupos(grupos,
                    new[] { new GradeSync.GrupoDesejado(1, "Memes", 0, new[] { a, b }) });
                view.UpdateLayout();

                Assert.Same(containerA, Descendants<ContentPresenter>(view).First(p => p.Content == a));
                Assert.Same(containerB, Descendants<ContentPresenter>(view).First(p => p.Content == b));
                Assert.Same(grupoContainer, Descendants<ContentPresenter>(view).First(p => p.Content == memes));

                if (!SystemParameters.ClientAreaAnimation)
                    return;

                a.Entrando = true;
                Pump(TimeSpan.FromMilliseconds(60));
                Assert.True(containerA.Opacity < 0.9,
                    $"o card editado deveria reanimar: {containerA.Opacity}");
                Assert.Equal(1, containerB.Opacity, 3); // vizinho intacto
                Assert.Equal(1, ((ScaleTransform)containerB.RenderTransform).ScaleX, 3);
                Assert.Equal(1, grupoContainer.Opacity, 3);

                a.Entrando = false;
                Pump(TimeSpan.FromMilliseconds(400));
                Assert.Equal(1, containerA.Opacity, 3);
                Assert.Equal(1, ((ScaleTransform)containerA.RenderTransform).ScaleX, 3);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static UserControl CarregarView()
    {
        var document = XDocument.Load(ViewPath());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        document.Root!.Attribute(x + "Class")!.Remove();
        foreach (var attr in document.Descendants().Attributes().Where(a =>
                     a.Name.LocalName.StartsWith("Preview") || a.Name.LocalName.StartsWith("Drag") ||
                     a.Name.LocalName is "Drop" or "Click").ToList())
            attr.Remove();
        foreach (var ns in document.Root.Attributes().Where(a => a.IsNamespaceDeclaration &&
                     a.Value.StartsWith("clr-namespace:") && !a.Value.Contains("assembly=")))
            ns.Value += ";assembly=SoundboardMic.App";
        return (UserControl)XamlReader.Parse(document.ToString());
    }

    private static Window CriarJanela(UserControl view) => new()
    {
        Content = view, Width = 900, Height = 700, ShowInTaskbar = false,
        WindowStyle = WindowStyle.None, Left = -10000, Top = -10000, ShowActivated = false
    };

    private static void Pump(TimeSpan duracao)
    {
        var fim = DateTime.UtcNow + duracao;
        while (DateTime.UtcNow < fim)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background, () => frame.Continue = false);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Sleep(15);
        }
    }

    private static string ViewPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "SoundboardMic.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return System.IO.Path.Combine(directory.FullName, "src", "SoundboardMic.App", "Views", "SoundboardView.xaml");
    }

    [Fact]
    public void Card_RenderizaMetadadosAoLadoDoIconeExcluirAbaixoDeEditarELoopCircular()
    {
        WpfTestHost.Run(() =>
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "SoundboardMic.sln")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            var document = XDocument.Load(System.IO.Path.Combine(directory.FullName,
                "src", "SoundboardMic.App", "Views", "SoundboardView.xaml"));
            XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var card = new XElement(document.Descendants(ns + "Border")
                .Single(e => (string?)e.Attribute("Style") == "{StaticResource SoundCard}"));
            foreach (var evt in card.Attributes().Where(a => a.Name.LocalName.StartsWith("Preview") ||
                         a.Name.LocalName.StartsWith("Drag") || a.Name.LocalName == "Drop").ToList())
                evt.Remove();
            card.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");

            var root = new Grid();
            root.Children.Add((Border)XamlReader.Parse(card.ToString()));
            root.DataContext = new AudioItemViewModel(
                new Audio { Id = 1, Nome = "Buzina", CaminhoArquivo = "buzina.wav", DuracaoMs = 3200, VolumePadrao = 0.8 }, null);
            root.Measure(new Size(400, 400));
            root.Arrange(new Rect(0, 0, 400, 400));
            root.UpdateLayout();

            var border = (Border)root.Children[0];
            var header = Descendants<Grid>(border).First(g => Grid.GetRow(g) == 0 && g.ColumnDefinitions.Count == 3);
            var icone = Descendants<Border>(header).First(b => b.Width == 36);
            var duracao = Descendants<TextBlock>(header).Single(t =>
                System.Windows.Data.BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path.Path == "DuracaoTexto");
            var editar = Descendants<Button>(header).Single(b => AutomationName(b) == "Editar áudio");
            var excluir = Descendants<Button>(header).Single(b => AutomationName(b) == "Excluir áudio");
            var cortar = Descendants<Button>(header).Single(b => AutomationName(b) == "Cortar áudio");

            Assert.True(Bounds(duracao, border).Left >= Bounds(icone, border).Right, "Metadados devem ficar à direita do ícone.");
            Assert.True(Bounds(duracao, border).Right <= Bounds(cortar, border).Left, "Metadados não podem invadir os botões.");
            Assert.True(Bounds(cortar, border).Right <= Bounds(editar, border).Left, "Cortar deve ficar ao lado de Editar.");
            Assert.Equal(Bounds(editar, border).Top, Bounds(cortar, border).Top, 1);
            Assert.True(Bounds(excluir, border).Top >= Bounds(editar, border).Bottom, "Excluir deve ficar abaixo de Editar.");

            var volume = Descendants<TextBlock>(header).Single(t =>
                System.Windows.Data.BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path.Path == "VolumeTexto");
            var centroMetadados = (Bounds(duracao, border).Top + Bounds(volume, border).Bottom) / 2;
            var centroIcone = (Bounds(icone, border).Top + Bounds(icone, border).Bottom) / 2;
            Assert.True(Math.Abs(centroMetadados - centroIcone) <= 2,
                $"Metadados devem ficar centralizados no ícone (centro {centroMetadados} vs {centroIcone}).");
            Assert.Equal(Bounds(icone, border).Top - 5, Bounds(editar, border).Top, 1);

            var loop = Descendants<ToggleButton>(border).Single();
            Assert.Single(Descendants<Ellipse>(loop));
            Assert.Equal(30, loop.ActualWidth);


        });
    }

    private static string AutomationName(DependencyObject d) =>
        System.Windows.Automation.AutomationProperties.GetName(d);

    private static Rect Bounds(FrameworkElement child, Visual ancestor) =>
        child.TransformToAncestor(ancestor).TransformBounds(new Rect(child.RenderSize));

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
