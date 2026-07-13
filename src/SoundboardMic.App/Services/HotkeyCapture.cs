using System.Windows.Input;
using SoundboardMic.Core.Hotkeys;

namespace SoundboardMic.App.Services;

/// <summary>
/// Converte um <see cref="KeyEventArgs"/> do WPF em um <see cref="KeyCombo"/> do domínio,
/// ignorando pressionamentos que são apenas modificadores.
/// </summary>
public static class HotkeyCapture
{
    public static KeyCombo? FromKeyEvent(KeyEventArgs e)
    {
        // System key (Alt) chega em e.SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (IsModifierKey(key))
            return null;

        var modifiers = KeyModifiers.None;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) modifiers |= KeyModifiers.Ctrl;
        if (m.HasFlag(ModifierKeys.Shift)) modifiers |= KeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Alt)) modifiers |= KeyModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Windows)) modifiers |= KeyModifiers.Win;

        var vk = (ushort)KeyInterop.VirtualKeyFromKey(key);
        return KeyComboParser.FromVirtualKey(vk, modifiers);
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin or
        Key.System;
}
