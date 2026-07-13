using NAudio.Wave;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

public class NoiseGateSampleProviderTests
{
    /// <summary>Fonte de teste: gera um valor constante em mono no sample rate indicado.</summary>
    private sealed class ConstantSource : ISampleProvider
    {
        private readonly float _value;

        public ConstantSource(int sampleRate, float value)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _value = value;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
                buffer[offset + i] = _value;
            return count;
        }
    }

    /// <summary>Fonte de teste: amplitude A nos primeiros N samples, depois amplitude B.</summary>
    private sealed class StepSource : ISampleProvider
    {
        private readonly float _first;
        private readonly float _second;
        private readonly int _stepAt;
        private int _position;

        public StepSource(int sampleRate, float first, float second, int stepAt)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _first = first;
            _second = second;
            _stepAt = stepAt;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
                buffer[offset + i] = _position++ < _stepAt ? _first : _second;
            return count;
        }
    }

    [Fact]
    public void SilencioAbaixoDoLimiar_SaidaFicaZerada()
    {
        // 0.001 ≈ -60 dB, bem abaixo do limiar padrão de -45 dB.
        var gate = new NoiseGateSampleProvider(new ConstantSource(48000, 0.001f));

        var buffer = new float[4800];
        gate.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.True(Math.Abs(sample) < 1e-4f));
    }

    [Fact]
    public void VozAcimaDoLimiar_PassaSemAtenuacao()
    {
        var gate = new NoiseGateSampleProvider(new ConstantSource(48000, 0.5f), attackMs: 1f);

        var buffer = new float[4800]; // 100 ms — muito além do attack de 1 ms
        gate.Read(buffer, 0, buffer.Length);

        // Depois do attack, o sinal passa intacto.
        Assert.Equal(0.5f, buffer[^1], precision: 3);
    }

    [Fact]
    public void Attack_RampaSobeSemDegrauAbrupto()
    {
        var source = new StepSource(48000, first: 0f, second: 0.5f, stepAt: 100);
        var gate = new NoiseGateSampleProvider(source); // attack padrão de 5 ms

        var buffer = new float[4800];
        gate.Read(buffer, 0, buffer.Length);

        // Abertura suave: nenhuma variação sample-a-sample maior que 1% do fundo de escala.
        for (var i = 1; i < buffer.Length; i++)
            Assert.True(Math.Abs(buffer[i] - buffer[i - 1]) < 0.01f,
                $"Degrau abrupto no sample {i}: {buffer[i - 1]} -> {buffer[i]}");

        // E no fim da rampa o sinal chega perto do original.
        Assert.Equal(0.5f, buffer[^1], precision: 2);
    }

    [Fact]
    public void HoldERelease_SeguraEDepoisFechaSuave()
    {
        const int sampleRate = 48000;
        const int vozSamples = 4800;
        var source = new StepSource(sampleRate, first: 0.5f, second: 0.001f, stepAt: vozSamples);
        var gate = new NoiseGateSampleProvider(source, attackMs: 1f, holdMs: 10f, releaseMs: 10f);

        // Fase de voz: gate abre por completo.
        var voz = new float[vozSamples];
        gate.Read(voz, 0, voz.Length);
        Assert.Equal(0.5f, voz[^1], precision: 3);

        // Fase de silêncio (0.001 abaixo do limiar).
        var silencio = new float[9600];
        gate.Read(silencio, 0, silencio.Length);

        // Durante o hold (10 ms = 480 samples) o gate segue aberto.
        Assert.Equal(0.001f, silencio[400], precision: 4);

        // Bem depois do hold + release, o gate fechou.
        Assert.True(Math.Abs(silencio[^1]) < 1e-5f);
    }

    [Fact]
    public void Disabled_BypassNaoAlteraSamples()
    {
        var gate = new NoiseGateSampleProvider(new ConstantSource(48000, 0.001f))
        {
            Enabled = false,
        };

        var buffer = new float[512];
        gate.Read(buffer, 0, buffer.Length);

        // Abaixo do limiar, mas o bypass deixa passar intacto.
        Assert.All(buffer, sample => Assert.Equal(0.001f, sample));
    }

    [Fact]
    public void ReligarDepoisDoBypass_ReabreLimpo()
    {
        var gate = new NoiseGateSampleProvider(new ConstantSource(48000, 0.5f), attackMs: 1f);

        var buffer = new float[4800];
        gate.Read(buffer, 0, buffer.Length); // abre o gate

        gate.Enabled = false;
        gate.Read(buffer, 0, buffer.Length); // bypass zera o envelope

        gate.Enabled = true;
        gate.Read(buffer, 0, buffer.Length);

        // Reabre a partir do zero (primeiro sample atenuado) e volta a passar no fim.
        Assert.True(Math.Abs(buffer[0]) < 0.05f);
        Assert.Equal(0.5f, buffer[^1], precision: 3);
    }

    [Fact]
    public void PreservaWaveFormatDaFonte()
    {
        var source = new ConstantSource(48000, 0.5f);
        var gate = new NoiseGateSampleProvider(source);

        Assert.Same(source.WaveFormat, gate.WaveFormat);
    }
}
