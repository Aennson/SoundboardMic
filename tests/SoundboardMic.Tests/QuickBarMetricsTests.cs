using SoundboardMic.App.Services;

namespace SoundboardMic.Tests;

public class QuickBarMetricsTests
{
    [Theory]
    [InlineData(1080, 1032, 48)]  // taskbar Win11 típica
    [InlineData(1080, 1040, 40)]  // taskbar compacta
    [InlineData(1080, 1000, 56)]  // taskbar gigante → cap no máximo
    [InlineData(1080, 1044, 40)]  // 36px reais → clamp no mínimo
    public void AlturaTaskbar_CalculaDaAreaDeTrabalho(double tela, double workBottom, double esperado)
    {
        Assert.Equal(esperado, QuickBarMetrics.AlturaTaskbar(tela, workBottom));
    }

    [Theory]
    [InlineData(1080, 1080)] // auto-hide: work area ocupa a tela toda
    [InlineData(1080, 1090)] // taskbar lateral/estados estranhos
    public void AlturaTaskbar_SemDiferenca_UsaFallback(double tela, double workBottom)
    {
        Assert.Equal(QuickBarMetrics.AlturaTaskbarFallback,
            QuickBarMetrics.AlturaTaskbar(tela, workBottom));
    }

    [Theory]
    [InlineData(48, 4, 38)]  // 48 - 8 - 2
    [InlineData(40, 4, 30)]
    [InlineData(56, 4, 40)]  // 46 → cap no máximo
    [InlineData(30, 4, 28)]  // 20 → clamp no mínimo
    public void TamanhoBotao_CabeNaBarra(double alturaBarra, double padding, double esperado)
    {
        Assert.Equal(esperado, QuickBarMetrics.TamanhoBotao(alturaBarra, padding));
    }

    [Fact]
    public void ClampPosicao_DentroDaTela_NaoMexe()
    {
        var (left, top) = QuickBarMetrics.ClampPosicao(100, 200, 300, 50, 0, 0, 1920, 1080);
        Assert.Equal(100, left);
        Assert.Equal(200, top);
    }

    [Theory]
    [InlineData(-500, 200, 0, 200)]      // fora pela esquerda
    [InlineData(5000, 200, 1620, 200)]   // fora pela direita (1920-300)
    [InlineData(100, -500, 100, 0)]      // fora por cima
    [InlineData(100, 5000, 100, 1030)]   // fora por baixo (1080-50)
    public void ClampPosicao_ForaDaTela_TrazDeVolta(double left, double top, double expLeft, double expTop)
    {
        var (l, t) = QuickBarMetrics.ClampPosicao(left, top, 300, 50, 0, 0, 1920, 1080);
        Assert.Equal(expLeft, l);
        Assert.Equal(expTop, t);
    }

    [Fact]
    public void ClampPosicao_MonitorSecundarioNegativo_RespeitaOrigemVirtual()
    {
        // Monitor secundário à esquerda: área virtual começa em -1920.
        var (l, t) = QuickBarMetrics.ClampPosicao(-1800, 100, 300, 50, -1920, 0, 3840, 1080);
        Assert.Equal(-1800, l);
        Assert.Equal(100, t);

        (l, t) = QuickBarMetrics.ClampPosicao(-5000, 100, 300, 50, -1920, 0, 3840, 1080);
        Assert.Equal(-1920, l);
    }

    [Fact]
    public void ClampPosicao_JanelaMaiorQueArea_FixaNaOrigem()
    {
        var (l, t) = QuickBarMetrics.ClampPosicao(500, 500, 3000, 2000, 0, 0, 1920, 1080);
        Assert.Equal(0, l);
        Assert.Equal(0, t);
    }
}
