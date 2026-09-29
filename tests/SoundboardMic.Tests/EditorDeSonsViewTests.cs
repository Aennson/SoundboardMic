using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using NAudio.Utils;
using NAudio.Wave;
using SoundboardMic.App.Controls;
using SoundboardMic.App.Services;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.Hotkeys;
using SoundboardMic.Core.Models;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

/// <summary>
/// Renderiza a aba "Editor de sons" de verdade: lista, onda e controles do trecho, e confere
/// que o teclado na onda move a seleção do view model.
/// </summary>
[Collection("WPF")]
public class EditorDeSonsViewTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"soundboardmic-editorview-{Guid.NewGuid():N}");

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public Task Editor_MostraOndaEAtendeTeclado() => WpfTestHost.RunAsync(async () =>
    {
        var audios = new AudioRepository(_db.Factory);
        var cache = new AudioFileCache(_dir);
        var settings = new SoundboardControllerTests.FakeSettings();
        var hook = new SoundboardControllerTests.FakeHook();
        var controller = new SoundboardController(new SoundboardControllerTests.FakeEngine(), hook,
            new HotkeyDispatcher(hook), new SoundboardControllerTests.FakeDevices(), audios,
            new MapeamentoRepository(_db.Factory), settings);
        using var vm = new EditorDeSonsViewModel(new MyInstantsViewModelTests.FakePlaybackService(),
            new AudioTrimService(audios, cache), controller, settings, new NullDialogs());

        using var ms = new MemoryStream();
        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(ms), new WaveFormat(8000, 16, 1)))
            for (var i = 0; i < 16000; i++) writer.WriteSample(i % 80 < 40 ? 0.5f : -0.5f);
        var (caminho, conteudo, nomeArquivo) = await cache.ImportarBytesAsync(ms.ToArray(), "buzina.wav");
        var audio = await audios.AddAsync(new Audio
        {
            Nome = "Buzina", CaminhoArquivo = caminho, ArquivoConteudo = conteudo,
            ArquivoNomeOriginal = nomeArquivo, DuracaoMs = 2000, VolumePadrao = 1,
        });
        var item = new AudioItemViewModel(audio, null);
        vm.Vincular(new ObservableCollection<AudioItemViewModel> { item, new(new Audio { Nome = "Grito" }, null) });

        var view = CarregarView();
        view.DataContext = vm;
        var window = new Window
        {
            Content = view, Width = 1100, Height = 760, ShowInTaskbar = false,
            WindowStyle = WindowStyle.None, Left = -10000, Top = -10000, ShowActivated = false
        };
        window.Show();
        try
        {
            vm.Abrir(item);
            for (var i = 0; i < 200 && (vm.CarregandoOnda || vm.Duracao == TimeSpan.Zero); i++)
                await Task.Delay(20);
            window.UpdateLayout();

            var lista = Descendentes<ListBox>(view).Single();
            Assert.Equal(2, lista.Items.Count);
            Assert.Same(item, lista.SelectedItem);

            var onda = Descendentes<WaveformView>(view).Single();
            Assert.True(onda.IsVisible);
            Assert.InRange(onda.ActualHeight, 130, 150);
            Assert.True(onda.ActualWidth > 300);
            Assert.NotNull(onda.Picos);
            Assert.Equal(vm.Duracao, onda.Duracao);
            Assert.True(onda.Focusable);
            Assert.False(string.IsNullOrWhiteSpace(AutomationPropertiesName(onda)));

            // Setas movem a alça ativa em 10 ms; Tab passa da alça inicial para a final.
            onda.Focus();
            Pressionar(onda, Key.Right);
            Pressionar(onda, Key.Right);
            Assert.Equal(TimeSpan.FromMilliseconds(20), vm.Inicio);
            Assert.Equal("0:00.020", vm.InicioTexto);

            Pressionar(onda, Key.Tab);
            Pressionar(onda, Key.Left);
            Assert.Equal(vm.Duracao - TimeSpan.FromMilliseconds(10), vm.Fim);
            Assert.True(vm.CriarNovoCommand.CanExecute(null));

            var botoes = Descendentes<Button>(view).Where(b => b.IsVisible)
                .Select(b => AutomationPropertiesName(b) is { Length: > 0 } n ? n : b.Content?.ToString()).ToList();
            Assert.Contains(botoes, b => b is not null && b.Contains("Criar", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(botoes, b => b is not null && b.Contains("Substituir", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            window.Close();
        }
    });

    private static void Pressionar(UIElement alvo, Key tecla)
    {
        var fonte = PresentationSource.FromVisual(alvo)!;
        alvo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, fonte, 0, tecla) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    private static string AutomationPropertiesName(DependencyObject d) =>
        System.Windows.Automation.AutomationProperties.GetName(d);

    private static IEnumerable<T> Descendentes<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is T t) yield return t;
            foreach (var d in Descendentes<T>(filho)) yield return d;
        }
    }

    private static UserControl CarregarView()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SoundboardMic.sln")))
            directory = directory.Parent;
        var path = Path.Combine(directory!.FullName, "src", "SoundboardMic.App", "Views", "EditorDeSonsView.xaml");

        var document = XDocument.Load(path);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        document.Root!.Attribute(x + "Class")!.Remove();
        foreach (var ns in document.Root.Attributes().Where(a => a.IsNamespaceDeclaration &&
                     a.Value.StartsWith("clr-namespace:") && !a.Value.Contains("assembly=")).ToList())
        {
            XNamespace antigo = ns.Value;
            XNamespace novo = ns.Value + ";assembly=SoundboardMic.App";
            foreach (var el in document.Descendants().Where(e => e.Name.Namespace == antigo).ToList())
                el.Name = novo + el.Name.LocalName;
            ns.Value = novo.NamespaceName;
        }
        return (UserControl)XamlReader.Parse(document.ToString());
    }

    private sealed class NullDialogs : IDialogService
    {
        public string? PickAudioFile() => null;
        public bool Confirm(string titulo, string mensagem) => false;
        public void Info(string titulo, string mensagem) { }
        public bool ShowAudioEditor(AudioEditViewModel viewModel) => false;
    }
}
