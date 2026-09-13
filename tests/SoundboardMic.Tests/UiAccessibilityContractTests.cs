using System.Xml.Linq;

namespace SoundboardMic.Tests;

public sealed class UiAccessibilityContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

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
    }

    [Fact]
    public void AudioEdit_ExposeTwoColumnPreviewAndVolumeTest()
    {
        var view = ReadXaml("src", "SoundboardMic.App", "Views", "AudioEditDialog.xaml");

        Assert.Contains("Width=\"680\"", view);
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
