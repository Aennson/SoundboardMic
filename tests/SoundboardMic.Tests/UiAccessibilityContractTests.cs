using System.Xml.Linq;

namespace SoundboardMic.Tests;

public sealed class UiAccessibilityContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Executable_EmbedsTheApplicationIcon()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "SoundboardMic.App.exe");
        using var embedded = System.Drawing.Icon.ExtractAssociatedIcon(executable);
        Assert.NotNull(embedded);
        using var expected = new System.Drawing.Icon(Path.Combine(RepositoryRoot,
            "src", "SoundboardMic.App", "Assets", "SoundboardMic.ico"), embedded.Size);
        using var actualBitmap = embedded.ToBitmap();
        using var expectedBitmap = expected.ToBitmap();
        Assert.Equal(expectedBitmap.Size, actualBitmap.Size);
        for (var y = 0; y < actualBitmap.Height; y++)
            for (var x = 0; x < actualBitmap.Width; x++)
                Assert.Equal(expectedBitmap.GetPixel(x, y), actualBitmap.GetPixel(x, y));
    }

    [Fact]
    public void Palette_ExposeFluentTokensAndFocusColor()
    {
        var palette = ReadXaml("src", "SoundboardMic.App", "Themes", "Palette.xaml");

        Assert.Contains("FluentAccentColor", palette);
        Assert.Contains("#4DD8E6", palette, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FocusBrush", palette);
        Assert.Contains("FluentSurfaceBrush", palette);
    }

    [Fact]
    public void Controls_ExposeKeyboardFocusCardsAndToggles()
    {
        var controls = ReadXaml("src", "SoundboardMic.App", "Themes", "Controls.xaml");

        Assert.Contains("x:Key=\"FocusVisualStyle\"", controls);
        Assert.Contains("x:Key=\"Card\"", controls);
        Assert.Contains("x:Key=\"ToggleSwitch\"", controls);
        Assert.Contains("FocusVisualStyle", controls);
    }

    [Fact]
    public void Soundboard_ExposeAudioRouteAndAccessibleActions()
    {
        var view = ReadXaml("src", "SoundboardMic.App", "Views", "SoundboardView.xaml");

        Assert.Contains("ROTA DE ÁUDIO", view);
        Assert.Contains("CABLE Input pronto", view);
        Assert.Contains("AutomationProperties.Name=\"Tocar áudio\"", view);
        Assert.Contains("AutomationProperties.Name=\"Parar áudio\"", view);
        Assert.Contains("AutomationProperties.Name=\"Editar áudio\"", view);
        Assert.Contains("AutomationProperties.Name=\"Repetir áudio em loop\"", view);
        var document = XDocument.Parse(view);
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var card = document.Descendants(ns + "Border")
            .Single(e => (string?)e.Attribute("Style") == "{StaticResource SoundCard}");
        Assert.Equal("190", (string?)card.Attribute("Width"));
        var actions = card.Descendants().Where(e => e.Attribute("CommandParameter") is not null &&
            new[] { "Tocar áudio", "Repetir áudio em loop", "Parar áudio" }
                .Contains((string?)e.Attribute("AutomationProperties.Name"))).ToList();
        Assert.Equal(3, actions.Count);
        Assert.Equal(new[] { "0", "1", "2" }, actions.Select(e => (string?)e.Attribute("Grid.Column")));
        Assert.All(actions, e =>
        {
            Assert.Equal("30", (string?)e.Attribute("Width"));
            Assert.Equal("30", (string?)e.Attribute("Height"));
        });
    }

    [Fact]
    public void Soundboard_CardTemExcluirAbaixoDeEditarMetadadosAoLadoDoIconeELoopCircular()
    {
        var view = ReadXaml("src", "SoundboardMic.App", "Views", "SoundboardView.xaml");
        var document = XDocument.Parse(view);
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var card = document.Descendants(ns + "Border")
            .Single(e => (string?)e.Attribute("Style") == "{StaticResource SoundCard}");
        var header = card.Descendants(ns + "Grid").First(e => (string?)e.Attribute("Grid.Row") == "0");

        var acoes = header.Descendants(ns + "Button").ToList();
        Assert.Equal(new[] { "Cortar áudio", "Editar áudio", "Excluir áudio" },
            acoes.Select(b => (string?)b.Attribute("AutomationProperties.Name")));
        Assert.Null(acoes[1].Parent!.Attribute("Orientation")); // StackPanel vertical: excluir abaixo
        Assert.Contains("CortarAudioCommand", (string?)acoes[0].Attribute("Command"));
        Assert.Contains("ExcluirAudioCommand", (string?)acoes[2].Attribute("Command"));

        var metadados = header.Elements(ns + "Grid").Single(e => (string?)e.Attribute("Grid.Column") == "1");
        Assert.Contains(metadados.Descendants(), e => (string?)e.Attribute("Text") == "{Binding DuracaoTexto}");
        Assert.Contains(metadados.Descendants(), e => (string?)e.Attribute("Text") == "{Binding VolumeTexto}");

        var loop = card.Descendants(ns + "ToggleButton").Single();
        Assert.Equal("{StaticResource LoopButton}", (string?)loop.Attribute("Style"));
        Assert.Contains("x:Key=\"LoopButton\"", ReadXaml("src", "SoundboardMic.App", "Themes", "Controls.xaml"));
    }

    [Fact]
    public void Soundboard_CardsReordenamPorArrastarSemBotoesDeMover()
    {
        var view = ReadXaml("src", "SoundboardMic.App", "Views", "SoundboardView.xaml");

        Assert.DoesNotContain("MoverAudioAcimaCommand", view);
        Assert.DoesNotContain("MoverAudioAbaixoCommand", view);
        Assert.Contains("MoverCategoriaAcimaCommand", view);
        Assert.Contains("MoverCategoriaAbaixoCommand", view);

        var document = XDocument.Parse(view);
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var card = document.Descendants(ns + "Border")
            .Single(e => (string?)e.Attribute("Style") == "{StaticResource SoundCard}");
        Assert.Equal("True", (string?)card.Attribute("AllowDrop"));
        Assert.Equal("Card_PreviewMouseMove", (string?)card.Attribute("PreviewMouseMove"));
        Assert.Equal("Card_Drop", (string?)card.Attribute("Drop"));
    }

    [Fact]
    public void AudioEdit_ExposeTwoColumnPreviewAndVolumeTest()
    {
        var view = ReadXaml("src", "SoundboardMic.App", "Views", "AudioEditDialog.xaml");

        Assert.Contains("Width=\"960\"", view);
        Assert.Contains("Height=\"880\"", view);
        Assert.Contains("PRÉVIA", view);
        Assert.Contains("Testar volume da prévia", view);
        Assert.Contains("Testar volume e ouvir prévia", view);
        Assert.Contains("Grid.Column=\"1\"", view);
    }

    [Fact]
    public void Settings_ExposeVuMeterAndCableIdentity()
    {
        var view = ReadXaml("src", "SoundboardMic.App", "Views", "SettingsView.xaml");

        Assert.Contains("x:Key=\"VuMeter\"", view);
        Assert.Contains("Nível do microfone", view);
        Assert.Contains("AutomationProperties.Name=\"Medidor de nível do microfone\"", view);
        Assert.Contains("{Binding CableNome}", view);
    }

    [Fact]
    public void QuickBar_ExposeDesignSystemSurfaceAndAccessibleActions()
    {
        var view = ReadXaml("src", "SoundboardMic.App", "Views", "QuickBarWindow.xaml");

        Assert.Contains("FluentSurfaceBrush", view);
        Assert.Contains("CornerCard", view);
        Assert.Contains("AutomationProperties.Name=\"{Binding Nome}\"", view);
        Assert.Contains("AutomationProperties.Name=\"Parar todos os sons\"", view);
    }

    private static string ReadXaml(params string[] path)
    {
        var file = Path.Combine(new[] { RepositoryRoot }.Concat(path).ToArray());
        var document = XDocument.Load(file, LoadOptions.PreserveWhitespace);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SoundboardMic.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Não foi possível localizar a raiz do repositório.");
    }
}
