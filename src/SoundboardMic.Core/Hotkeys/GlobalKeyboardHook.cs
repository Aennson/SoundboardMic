using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoundboardMic.Core.Hotkeys;

/// <summary>
/// Implementação de <see cref="IGlobalKeyboardHook"/> com WH_KEYBOARD_LL
/// (<c>SetWindowsHookEx</c>). Rastreia o estado dos modificadores a partir do
/// próprio fluxo de eventos e monta um <see cref="KeyCombo"/> a cada tecla
/// principal pressionada.
/// </summary>
public sealed class GlobalKeyboardHook : IGlobalKeyboardHook
{
    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104; // com Alt pressionado
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;

    // Virtual-key codes dos modificadores.
    private const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4, VK_RMENU = 0xA5;   // Alt
    private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    // Mantém o delegate vivo enquanto o hook está instalado (evita GC do callback).
    private readonly LowLevelKeyboardProc _proc;
    private readonly object _lock = new();
    private IntPtr _hookHandle = IntPtr.Zero;

    public GlobalKeyboardHook() => _proc = HookCallback;

    public bool IsInstalled
    {
        get { lock (_lock) return _hookHandle != IntPtr.Zero; }
    }

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Install()
    {
        lock (_lock)
        {
            if (_hookHandle != IntPtr.Zero)
                return;

            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule!;
            var moduleHandle = GetModuleHandle(module.ModuleName);

            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);
            if (_hookHandle == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Falha ao instalar o hook de teclado (erro {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Uninstall()
    {
        lock (_lock)
        {
            if (_hookHandle == IntPtr.Zero)
                return;
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Uninstall();
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var message = (int)wParam;
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var vk = (int)data.vkCode;

            if (message is WM_KEYDOWN or WM_SYSKEYDOWN)
            {
                if (!IsModifierKey(vk))
                {
                    var combo = KeyComboParser.FromVirtualKey((ushort)vk, GetCurrentModifiers());
                    if (combo is not null && RaiseAndShouldSwallow(combo))
                        return 1; // consome a tecla (não repassa ao app em foco)
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private bool RaiseAndShouldSwallow(KeyCombo combo)
    {
        var handler = HotkeyPressed;
        if (handler is null)
            return false;

        var args = new HotkeyPressedEventArgs { Combo = combo };
        handler(this, args);
        return args.Handled;
    }

    /// <summary>Lê o estado atual dos modificadores via GetAsyncKeyState.</summary>
    private static KeyModifiers GetCurrentModifiers()
    {
        var mods = KeyModifiers.None;
        if (IsDown(VK_LCONTROL) || IsDown(VK_RCONTROL)) mods |= KeyModifiers.Ctrl;
        if (IsDown(VK_LSHIFT) || IsDown(VK_RSHIFT)) mods |= KeyModifiers.Shift;
        if (IsDown(VK_LMENU) || IsDown(VK_RMENU)) mods |= KeyModifiers.Alt;
        if (IsDown(VK_LWIN) || IsDown(VK_RWIN)) mods |= KeyModifiers.Win;
        return mods;
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool IsModifierKey(int vk) => vk is
        VK_LSHIFT or VK_RSHIFT or VK_LCONTROL or VK_RCONTROL or
        VK_LMENU or VK_RMENU or VK_LWIN or VK_RWIN or
        0x10 or 0x11 or 0x12; // VK_SHIFT / VK_CONTROL / VK_MENU genéricos

    // ---- P/Invoke ----

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
