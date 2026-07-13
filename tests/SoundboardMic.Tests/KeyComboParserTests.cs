using SoundboardMic.Core.Hotkeys;

namespace SoundboardMic.Tests;

public class KeyComboParserTests
{
    [Theory]
    [InlineData("Ctrl+Alt+F1", KeyModifiers.Ctrl | KeyModifiers.Alt, "F1")]
    [InlineData("Ctrl+Shift+3", KeyModifiers.Ctrl | KeyModifiers.Shift, "3")]
    [InlineData("F5", KeyModifiers.None, "F5")]
    [InlineData("Win+Space", KeyModifiers.Win, "Space")]
    [InlineData("Ctrl+NumPad5", KeyModifiers.Ctrl, "NumPad5")]
    public void TryParse_CombinacoesValidas(string texto, KeyModifiers mods, string tecla)
    {
        Assert.True(KeyComboParser.TryParse(texto, out var combo, out var erro));
        Assert.Null(erro);
        Assert.Equal(mods, combo!.Modifiers);
        Assert.Equal(tecla, combo.Key);
    }

    [Theory]
    [InlineData("control + alt + f1", "Ctrl+Alt+F1")]  // aliases + casing + espaços
    [InlineData("SHIFT+a", "Shift+A")]
    [InlineData("windows+esc", "Win+Escape")]
    [InlineData("alt+ctrl+f2", "Ctrl+Alt+F2")]         // ordem canônica reordena
    public void TryParse_NormalizaParaCanonico(string entrada, string canonicoEsperado)
    {
        Assert.True(KeyComboParser.TryParse(entrada, out var combo, out _));
        Assert.Equal(canonicoEsperado, combo!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Alt")]           // só modificadores, sem tecla principal
    [InlineData("Ctrl+F1+F2")]         // duas teclas principais
    [InlineData("Ctrl+Ctrl+A")]        // modificador repetido
    [InlineData("Ctrl++A")]            // segmento vazio
    [InlineData("Ctrl+Banana")]        // tecla desconhecida
    public void TryParse_CombinacoesInvalidas_RetornamErro(string texto)
    {
        Assert.False(KeyComboParser.TryParse(texto, out var combo, out var erro));
        Assert.Null(combo);
        Assert.False(string.IsNullOrWhiteSpace(erro));
    }

    [Fact]
    public void Parse_TextoInvalido_Lanca()
    {
        Assert.Throws<FormatException>(() => KeyComboParser.Parse("Ctrl+Alt"));
    }

    [Fact]
    public void ToVirtualKey_EFromVirtualKey_SaoInversas()
    {
        var original = KeyComboParser.Parse("Ctrl+Alt+F1");

        var vk = KeyComboParser.ToVirtualKey(original);
        var reconstruido = KeyComboParser.FromVirtualKey(vk, original.Modifiers);

        Assert.Equal(original, reconstruido);
    }

    [Fact]
    public void FromVirtualKey_CodigoDesconhecido_RetornaNull()
    {
        // 0xFF não está na tabela de teclas suportadas.
        Assert.Null(KeyComboParser.FromVirtualKey(0xFF, KeyModifiers.None));
    }

    [Fact]
    public void Equality_IndependeDeComoFoiCriado()
    {
        var a = KeyComboParser.Parse("ctrl+shift+a");
        var b = new KeyCombo(KeyModifiers.Ctrl | KeyModifiers.Shift, "A");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_UsadoComoChaveDoBanco_EhEstavel()
    {
        var combo = KeyComboParser.Parse("alt+ctrl+f1");
        var roundtrip = KeyComboParser.Parse(combo.ToString());

        Assert.Equal(combo, roundtrip);
        Assert.Equal("Ctrl+Alt+F1", combo.ToString());
    }
}
