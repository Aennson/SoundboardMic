namespace SoundboardMic.Core.Hotkeys;

/// <summary>
/// Converte combinações de teclas entre texto ("Ctrl+Alt+F1"), <see cref="KeyCombo"/>
/// e virtual-key codes do Windows. A forma canônica ordena os modificadores como
/// Ctrl, Shift, Alt, Win e usa os nomes de tecla da tabela interna.
/// </summary>
public static class KeyComboParser
{
    // Aliases aceitos no parse (case-insensitive) → modificador.
    private static readonly Dictionary<string, KeyModifiers> ModifierAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl"] = KeyModifiers.Ctrl,
            ["Control"] = KeyModifiers.Ctrl,
            ["Shift"] = KeyModifiers.Shift,
            ["Alt"] = KeyModifiers.Alt,
            ["Win"] = KeyModifiers.Win,
            ["Windows"] = KeyModifiers.Win,
            ["Super"] = KeyModifiers.Win,
        };

    // Nome canônico → virtual-key code (Winuser.h).
    private static readonly Dictionary<string, ushort> NameToVk = BuildNameToVk();

    // Reverso: virtual-key code → nome canônico.
    private static readonly Dictionary<ushort, string> VkToName =
        NameToVk.ToDictionary(kv => kv.Value, kv => kv.Key);

    // Aliases de tecla principal aceitos no parse → nome canônico.
    private static readonly Dictionary<string, string> KeyAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Esc"] = "Escape",
            ["Return"] = "Enter",
            ["Del"] = "Delete",
            ["Ins"] = "Insert",
            ["PgUp"] = "PageUp",
            ["PgDn"] = "PageDown",
            ["Spacebar"] = "Space",
        };

    private static Dictionary<string, ushort> BuildNameToVk()
    {
        var map = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

        for (var c = 'A'; c <= 'Z'; c++)
            map[c.ToString()] = (ushort)c;                       // 0x41–0x5A
        for (var d = '0'; d <= '9'; d++)
            map[d.ToString()] = (ushort)d;                       // 0x30–0x39
        for (var f = 1; f <= 24; f++)
            map[$"F{f}"] = (ushort)(0x70 + f - 1);               // F1=0x70
        for (var n = 0; n <= 9; n++)
            map[$"NumPad{n}"] = (ushort)(0x60 + n);              // NumPad0=0x60

        map["NumPadMultiply"] = 0x6A;
        map["NumPadAdd"] = 0x6B;
        map["NumPadSubtract"] = 0x6D;
        map["NumPadDecimal"] = 0x6E;
        map["NumPadDivide"] = 0x6F;

        map["Up"] = 0x26;
        map["Down"] = 0x28;
        map["Left"] = 0x25;
        map["Right"] = 0x27;

        map["Space"] = 0x20;
        map["Enter"] = 0x0D;
        map["Tab"] = 0x09;
        map["Escape"] = 0x1B;
        map["Backspace"] = 0x08;
        map["Delete"] = 0x2E;
        map["Insert"] = 0x2D;
        map["Home"] = 0x24;
        map["End"] = 0x23;
        map["PageUp"] = 0x21;
        map["PageDown"] = 0x22;
        map["PrintScreen"] = 0x2C;
        map["Pause"] = 0x13;
        map["ScrollLock"] = 0x91;

        return map;
    }

    /// <summary>Nomes de tecla principal válidos (canônicos), para a UI listar.</summary>
    public static IReadOnlyCollection<string> KnownKeys => NameToVk.Keys;

    /// <summary>
    /// Tenta interpretar um texto como combinação ("ctrl + alt + f1" é aceito;
    /// a saída é sempre canônica). Regras: no máximo uma tecla principal,
    /// obrigatória; modificadores não podem se repetir.
    /// </summary>
    public static bool TryParse(string? text, out KeyCombo? combo, out string? erro)
    {
        combo = null;
        erro = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            erro = "Combinação vazia.";
            return false;
        }

        var modifiers = KeyModifiers.None;
        string? key = null;

        foreach (var raw in text.Split('+'))
        {
            var token = raw.Trim();
            if (token.Length == 0)
            {
                erro = "Combinação contém um segmento vazio (verifique os '+').";
                return false;
            }

            if (ModifierAliases.TryGetValue(token, out var modifier))
            {
                if (modifiers.HasFlag(modifier))
                {
                    erro = $"Modificador repetido: {modifier}.";
                    return false;
                }
                modifiers |= modifier;
                continue;
            }

            var canonical = KeyAliases.TryGetValue(token, out var alias) ? alias : token;
            if (!NameToVk.TryGetValue(canonical, out _))
            {
                erro = $"Tecla desconhecida: \"{token}\".";
                return false;
            }
            if (key is not null)
            {
                erro = $"Mais de uma tecla principal (\"{key}\" e \"{canonical}\").";
                return false;
            }
            // Normaliza para o casing canônico da tabela.
            key = NameToVk.Keys.First(k => string.Equals(k, canonical, StringComparison.OrdinalIgnoreCase));
        }

        if (key is null)
        {
            erro = "A combinação precisa de uma tecla principal (ex.: F1, A, NumPad5).";
            return false;
        }

        combo = new KeyCombo(modifiers, key);
        return true;
    }

    /// <summary>Versão que lança <see cref="FormatException"/> em texto inválido.</summary>
    public static KeyCombo Parse(string text)
    {
        if (!TryParse(text, out var combo, out var erro))
            throw new FormatException($"Combinação de teclas inválida: {erro}");
        return combo!;
    }

    /// <summary>Serialização canônica: Ctrl+Shift+Alt+Win+Tecla.</summary>
    public static string ToCanonicalString(KeyCombo combo)
    {
        var parts = new List<string>(5);
        if (combo.Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (combo.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (combo.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (combo.Modifiers.HasFlag(KeyModifiers.Win)) parts.Add("Win");
        parts.Add(combo.Key);
        return string.Join("+", parts);
    }

    /// <summary>
    /// Cria um combo a partir de um virtual-key code (hook de teclado).
    /// Retorna null para teclas fora da tabela (multimídia, OEM etc.).
    /// </summary>
    public static KeyCombo? FromVirtualKey(ushort vkCode, KeyModifiers modifiers) =>
        VkToName.TryGetValue(vkCode, out var name) ? new KeyCombo(modifiers, name) : null;

    /// <summary>Virtual-key code da tecla principal de um combo.</summary>
    public static ushort ToVirtualKey(KeyCombo combo) => NameToVk[combo.Key];
}
