namespace SoundboardMic.App.Services;

/// <summary>
/// Cálculos de tamanho/posição da barra rápida. Funções puras (só doubles)
/// para serem testáveis sem WPF; o chamador alimenta com SystemParameters.
/// </summary>
public static class QuickBarMetrics
{
    public const double AlturaTaskbarFallback = 48;
    public const double AlturaTaskbarMin = 40;
    public const double AlturaTaskbarMax = 56;

    public const double BotaoMin = 28;
    public const double BotaoMax = 40;

    /// <summary>
    /// Altura da taskbar em DIPs (tela cheia menos área de trabalho).
    /// Taskbar em auto-hide ou lateral produz diferença ≤ 0 → fallback.
    /// </summary>
    public static double AlturaTaskbar(double screenHeight, double workAreaBottom)
    {
        var diff = screenHeight - workAreaBottom;
        if (diff <= 0)
            return AlturaTaskbarFallback;
        return Math.Clamp(diff, AlturaTaskbarMin, AlturaTaskbarMax);
    }

    /// <summary>
    /// Lado do botão para a barra caber na altura dada (descontando padding
    /// e a borda de 1px de cada lado da pílula).
    /// </summary>
    public static double TamanhoBotao(double alturaBarra, double padding)
    {
        return Math.Clamp(alturaBarra - 2 * padding - 2, BotaoMin, BotaoMax);
    }

    /// <summary>Mantém a janela dentro da área virtual combinada dos monitores.</summary>
    public static (double Left, double Top) ClampPosicao(
        double left, double top, double largura, double altura,
        double virtLeft, double virtTop, double virtWidth, double virtHeight)
    {
        var maxLeft = Math.Max(virtLeft, virtLeft + virtWidth - largura);
        var maxTop = Math.Max(virtTop, virtTop + virtHeight - altura);
        return (Math.Clamp(left, virtLeft, maxLeft), Math.Clamp(top, virtTop, maxTop));
    }
}
