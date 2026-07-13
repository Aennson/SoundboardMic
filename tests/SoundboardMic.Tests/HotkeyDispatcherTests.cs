using SoundboardMic.Core.Hotkeys;

namespace SoundboardMic.Tests;

public class HotkeyDispatcherTests
{
    /// <summary>Hook falso: permite simular pressionamentos sem win32.</summary>
    private sealed class FakeHook : IGlobalKeyboardHook
    {
        public bool IsInstalled { get; private set; }
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

        public void Install() => IsInstalled = true;
        public void Uninstall() => IsInstalled = false;
        public void Dispose() { }

        /// <summary>Simula um pressionamento e devolve se foi consumido (Handled).</summary>
        public bool Press(string combo)
        {
            var args = new HotkeyPressedEventArgs { Combo = KeyComboParser.Parse(combo) };
            HotkeyPressed?.Invoke(this, args);
            return args.Handled;
        }
    }

    private static HotkeyAction Signaling(ManualResetEventSlim evt, Action? body = null)
        => () => { body?.Invoke(); evt.Set(); };

    [Fact]
    public void ComboRegistrado_ExecutaAcaoEConsomeTecla()
    {
        var hook = new FakeHook();
        using var dispatcher = new HotkeyDispatcher(hook);
        using var executed = new ManualResetEventSlim();

        dispatcher.Bind(KeyComboParser.Parse("Ctrl+Alt+F1"), Signaling(executed));

        var handled = hook.Press("Ctrl+Alt+F1");

        Assert.True(handled);
        Assert.True(executed.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void ComboNaoRegistrado_NaoConsomeTecla()
    {
        var hook = new FakeHook();
        using var dispatcher = new HotkeyDispatcher(hook);

        dispatcher.Bind(KeyComboParser.Parse("Ctrl+Alt+F1"), () => { });

        Assert.False(hook.Press("Ctrl+Alt+F2"));
    }

    [Fact]
    public void PanicKey_TemPrioridadeSobreBindingNormal()
    {
        var hook = new FakeHook();
        using var dispatcher = new HotkeyDispatcher(hook);
        using var panicRan = new ManualResetEventSlim();
        var normalRan = false;

        var combo = KeyComboParser.Parse("Ctrl+Alt+P");
        dispatcher.Bind(combo, () => normalRan = true);
        dispatcher.SetPanicKey(combo, Signaling(panicRan));

        Assert.True(hook.Press("Ctrl+Alt+P"));
        Assert.True(panicRan.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(normalRan);
    }

    [Fact]
    public void SetBindings_SubstituiConjuntoInteiro()
    {
        var hook = new FakeHook();
        using var dispatcher = new HotkeyDispatcher(hook);
        using var novoRan = new ManualResetEventSlim();

        dispatcher.Bind(KeyComboParser.Parse("Ctrl+1"), () => { });
        dispatcher.SetBindings(new[]
        {
            new HotkeyBinding(KeyComboParser.Parse("Ctrl+2"), Signaling(novoRan)),
        });

        Assert.False(hook.Press("Ctrl+1")); // antigo removido
        Assert.True(hook.Press("Ctrl+2"));  // novo ativo
        Assert.True(novoRan.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Unbind_RemoveAcao()
    {
        var hook = new FakeHook();
        using var dispatcher = new HotkeyDispatcher(hook);
        var combo = KeyComboParser.Parse("Ctrl+Alt+F1");

        dispatcher.Bind(combo, () => { });
        dispatcher.Unbind(combo);

        Assert.False(hook.Press("Ctrl+Alt+F1"));
    }

    [Fact]
    public void ActiveCombos_IncluiBindingsEPanic()
    {
        var hook = new FakeHook();
        using var dispatcher = new HotkeyDispatcher(hook);

        dispatcher.Bind(KeyComboParser.Parse("Ctrl+1"), () => { });
        dispatcher.SetPanicKey(KeyComboParser.Parse("Ctrl+0"), () => { });

        var combos = dispatcher.ActiveCombos;

        Assert.Contains(KeyComboParser.Parse("Ctrl+1"), combos);
        Assert.Contains(KeyComboParser.Parse("Ctrl+0"), combos);
    }

    [Fact]
    public void AcaoComExcecao_NaoDerrubaODispatcher()
    {
        var hook = new FakeHook();
        using var dispatcher = new HotkeyDispatcher(hook);
        using var segundaRan = new ManualResetEventSlim();

        dispatcher.Bind(KeyComboParser.Parse("Ctrl+1"), () => throw new InvalidOperationException());
        dispatcher.Bind(KeyComboParser.Parse("Ctrl+2"), Signaling(segundaRan));

        hook.Press("Ctrl+1"); // não deve propagar exceção
        Assert.True(hook.Press("Ctrl+2"));
        Assert.True(segundaRan.Wait(TimeSpan.FromSeconds(2)));
    }
}
