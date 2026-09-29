using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

public class WaveformPeaksTests
{
    /// <summary>Fonte estéreo em memória: canal esquerdo em silêncio, direito com uma rampa.</summary>
    private sealed class FonteMemoria : ISampleProvider
    {
        private readonly float[] _amostras;
        private int _pos;

        public FonteMemoria(float[] amostras, int taxa, int canais)
        {
            _amostras = amostras;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(taxa, canais);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            var n = Math.Min(count, _amostras.Length - _pos);
            Array.Copy(_amostras, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
    }

    [Fact]
    public void Calcular_PicoPorColuna_CombinaCanais()
    {
        // 4 colunas de 100 frames estéreo; o pico de cada coluna está só no canal direito.
        var taxa = 400;
        var amostras = new float[400 * 2];
        float[] picos = { 0.1f, -0.8f, 0.4f, 1f };
        for (var col = 0; col < 4; col++)
            amostras[(col * 100 + 50) * 2 + 1] = picos[col];

        var onda = WaveformPeaks.Calcular(new FonteMemoria(amostras, taxa, 2), 400, 4);

        Assert.Equal(new[] { 0.1f, 0.8f, 0.4f, 1f }, onda.Picos);
        Assert.Equal(TimeSpan.FromSeconds(1), onda.Duracao);
        Assert.Equal(2, onda.Canais);
        Assert.Equal(taxa, onda.TaxaAmostragem);
    }

    [Fact]
    public void Calcular_EstimativaMaiorQueOReal_UsaFramesLidos()
    {
        var amostras = Enumerable.Repeat(0.5f, 1000).ToArray();

        var onda = WaveformPeaks.Calcular(new FonteMemoria(amostras, 1000, 1), totalFrames: 4000, colunas: 8);

        Assert.Equal(TimeSpan.FromSeconds(1), onda.Duracao);
        Assert.All(onda.Picos, p => Assert.Equal(0.5f, p, 3));
    }

    [Fact]
    public void Calcular_Cancelado_Lanca()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var fonte = new SignalGenerator(8000, 1) { Type = SignalGeneratorType.Sin }.Take(TimeSpan.FromSeconds(2));

        Assert.ThrowsAny<OperationCanceledException>(() => WaveformPeaks.Calcular(fonte, 16000, 64, cts.Token));
    }

    [Fact]
    public void Reamostrar_UsaMaximoPorFaixa()
    {
        var onda = new WaveformPeaks(new[] { 0.1f, 0.9f, 0.2f, 0.3f, 0.7f, 0.1f }, TimeSpan.FromSeconds(1), 8000, 1);

        Assert.Equal(new[] { 0.9f, 0.3f, 0.7f }, onda.Reamostrar(3));
        Assert.Equal(10, onda.Reamostrar(10).Length);
    }
}

public class SelecaoCorteTests
{
    private static readonly TimeSpan Dur = TimeSpan.FromSeconds(10);
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Tudo_CobreOAudioInteiro()
    {
        var s = SelecaoCorte.Tudo(Dur);
        Assert.True(s.CobreTudo(Dur));
        Assert.Equal(Dur, s.Duracao);
        Assert.False(new SelecaoCorte(Ms(10), Dur).CobreTudo(Dur));
    }

    [Fact]
    public void ComInicio_NaoUltrapassaOMinimoAntesDoFim()
    {
        var s = new SelecaoCorte(TimeSpan.Zero, Ms(1000));

        Assert.Equal(Ms(900), s.ComInicio(Ms(990), Dur).Inicio);
        Assert.Equal(TimeSpan.Zero, s.ComInicio(Ms(-50), Dur).Inicio);
        Assert.Equal(Ms(300), s.ComInicio(Ms(300), Dur).Inicio);
    }

    [Fact]
    public void ComFim_RespeitaMinimoEDuracao()
    {
        var s = new SelecaoCorte(Ms(500), Dur);

        Assert.Equal(Ms(600), s.ComFim(Ms(510), Dur).Fim);
        Assert.Equal(Dur, s.ComFim(Ms(20000), Dur).Fim);
    }

    [Fact]
    public void Entre_OrdenaEGaranteMinimoDentroDoAudio()
    {
        Assert.Equal(new SelecaoCorte(Ms(200), Ms(800)), SelecaoCorte.Entre(Ms(800), Ms(200), Dur));
        Assert.Equal(new SelecaoCorte(Ms(9900), Dur), SelecaoCorte.Entre(Dur, Dur, Dur));
        Assert.Equal(new SelecaoCorte(Ms(500), Ms(600)), SelecaoCorte.Entre(Ms(500), Ms(520), Dur));
    }

    [Fact]
    public void AudioMenorQueOMinimo_SelecionaTudo()
    {
        var curto = Ms(60);
        Assert.Equal(new SelecaoCorte(TimeSpan.Zero, curto), SelecaoCorte.Entre(Ms(10), Ms(20), curto));
    }

    [Theory]
    [InlineData("1:05.250", 65250)]
    [InlineData("0:03", 3000)]
    [InlineData("12.5", 12500)]
    [InlineData("12,5", 12500)]
    [InlineData(" 2:00.001 ", 120001)]
    public void TryParse_FormatosAceitos(string texto, double ms)
    {
        Assert.True(SelecaoCorte.TryParse(texto, out var t));
        Assert.Equal(Ms(ms), t);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1:75")]
    [InlineData("1:2:3")]
    [InlineData("-3")]
    public void TryParse_Invalidos(string texto)
    {
        Assert.False(SelecaoCorte.TryParse(texto, out _));
    }

    [Fact]
    public void Formatar_MinutosSegundosMilissegundos()
    {
        Assert.Equal("1:05.250", SelecaoCorte.Formatar(Ms(65250)));
        Assert.Equal("0:00.000", SelecaoCorte.Formatar(Ms(-5)));
    }
}
