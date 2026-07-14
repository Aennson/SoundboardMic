using System.Globalization;

namespace SoundboardMic.App.Services;

/// <summary>Um glifo selecionável do Segoe Fluent Icons (código hex + nome amigável).</summary>
public sealed record IconOption(string Codigo, string Nome);

/// <summary>Uma cor de fundo selecionável ("#RRGGBB" + nome amigável).</summary>
public sealed record CorOption(string Hex, string Nome);

/// <summary>
/// Catálogo curado de glifos e cores para o botão de cada áudio na barra rápida.
/// Os code-points são armazenados como hex ("E8D6") no banco; a conversão para
/// caractere acontece só em runtime (glifos nunca vão literais para XAML).
/// </summary>
public static class IconCatalog
{
    public const string GlifoPadrao = "E8D6"; // nota musical
    public const string CorPadrao = "#7C5CFF"; // roxo (accent)

    public static IReadOnlyList<IconOption> Glifos { get; } = new IconOption[]
    {
        new("E8D6", "Nota musical"),
        new("E767", "Volume"),
        new("E720", "Microfone"),
        new("E7F6", "Fones de ouvido"),
        new("E768", "Play"),
        new("E769", "Pausa"),
        new("E71A", "Parar"),
        new("E765", "Teclado"),
        new("E76E", "Sorriso"),
        new("E73E", "Certo"),
        new("E711", "X"),
        new("E783", "Exclamação"),
        new("E7BA", "Aviso"),
        new("E734", "Estrela"),
        new("E735", "Estrela cheia"),
        new("EB52", "Coração"),
        new("EB51", "Coração cheio"),
        new("E787", "Calendário"),
        new("E823", "Relógio"),
        new("E8BD", "Comentário"),
        new("E77B", "Pessoa"),
        new("E716", "Pessoas"),
        new("E80F", "Casa"),
        new("E706", "Sol"),
        new("E708", "Lua"),
        new("E718", "Alfinete"),
        new("E721", "Lupa"),
        new("E7C1", "Bandeira"),
        new("E945", "Raio"),
        new("E7FC", "Controle"),
        new("E709", "Avião"),
        new("E774", "Globo"),
    };

    public static IReadOnlyList<CorOption> Cores { get; } = new CorOption[]
    {
        new("#7C5CFF", "Roxo"),
        new("#5B8DEF", "Azul"),
        new("#2AC3C3", "Ciano"),
        new("#3DD68C", "Verde"),
        new("#FFB454", "Âmbar"),
        new("#FF8A5C", "Laranja"),
        new("#FF5C6C", "Vermelho"),
        new("#F06BB3", "Rosa"),
        new("#6B6B7E", "Cinza"),
    };

    /// <summary>Converte "E8D6" → caractere do glifo; null/inválido cai no padrão.</summary>
    public static string GlyphChar(string? codigo)
    {
        if (!string.IsNullOrWhiteSpace(codigo) &&
            int.TryParse(codigo, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp) &&
            cp is >= 0xE000 and <= 0xFFFD)
        {
            return char.ConvertFromUtf32(cp);
        }
        return char.ConvertFromUtf32(0xE8D6);
    }
}
