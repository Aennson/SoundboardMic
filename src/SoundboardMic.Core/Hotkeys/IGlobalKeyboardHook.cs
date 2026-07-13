namespace SoundboardMic.Core.Hotkeys;

/// <summary>Argumentos de um pressionamento de tecla capturado pelo hook global.</summary>
public class HotkeyPressedEventArgs : EventArgs
{
    public required KeyCombo Combo { get; init; }

    /// <summary>
    /// Se definido como true pelo handler, o hook consome o evento e a tecla NÃO
    /// é entregue ao aplicativo em foco (evita que o atalho "vaze" para o jogo/app).
    /// </summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Hook global de teclado de baixo nível (funciona com o app minimizado/sem foco).
/// Dispara <see cref="HotkeyPressed"/> a cada combinação reconhecível pressionada.
/// </summary>
public interface IGlobalKeyboardHook : IDisposable
{
    bool IsInstalled { get; }

    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <summary>Instala o hook (WH_KEYBOARD_LL). Idempotente.</summary>
    void Install();

    /// <summary>Remove o hook. Idempotente.</summary>
    void Uninstall();
}
