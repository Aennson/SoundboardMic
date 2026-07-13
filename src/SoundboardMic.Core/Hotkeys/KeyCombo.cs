namespace SoundboardMic.Core.Hotkeys;

/// <summary>Modificadores de uma combinação de teclas.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}

/// <summary>
/// Combinação de teclas imutável: modificadores + uma tecla principal canônica
/// (ex.: "F1", "A", "NumPad5"). Use <see cref="KeyComboParser"/> para criar a
/// partir de texto ("Ctrl+Alt+F1") ou de um virtual-key code.
/// </summary>
/// <param name="Modifiers">Modificadores pressionados.</param>
/// <param name="Key">Nome canônico da tecla principal.</param>
public record KeyCombo(KeyModifiers Modifiers, string Key)
{
    /// <summary>Forma serializada canônica, ex.: "Ctrl+Alt+F1" (a que vai para o banco).</summary>
    public override string ToString() => KeyComboParser.ToCanonicalString(this);
}
