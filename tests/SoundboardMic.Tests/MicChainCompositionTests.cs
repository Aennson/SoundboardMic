using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

/// <summary>
/// Replica a composição do ramo do mic feita pelo MicInjectionEngine
/// (sem WASAPI): fonte qualquer → 48 kHz mono → rnnoise → gate → estéreo,
/// e valida formato final e o efeito dos toggles.
/// </summary>
public class MicChainCompositionTests
{
    private static readonly WaveFormat MonoMixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

    private sealed class ConstantSource : ISampleProvider
    {
        private readonly float _value;

        public ConstantSource(int sampleRate, int channels, float value)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
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

    /// <summary>Fake do denoiser: atenua pela metade, para o efeito ser observável.</summary>
    private sealed class HalvingProcessor : IRnNoiseProcessor
    {
        public void Process(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
                buffer[offset + i] *= 0.5f;
        }

        public void Reset() { }
        public void Dispose() { }
    }

    private static (RnNoiseSampleProvider RnNoise, NoiseGateSampleProvider Gate, ISampleProvider Chain)
        BuildChain(ISampleProvider micSource)
    {
        var mono = SampleProviderConverter.ConvertToFormat(micSource, MonoMixFormat);
        var rnNoise = new RnNoiseSampleProvider(mono, new HalvingProcessor());
        var gate = new NoiseGateSampleProvider(rnNoise, attackMs: 1f);
        var stereo = new MonoToStereoSampleProvider(gate);
        return (rnNoise, gate, stereo);
    }

    [Fact]
    public void CadeiaCompleta_ProduzEstereo48kFloat()
    {
        // Mic típico: estéreo 44.1k, como alguns drivers expõem.
        var (_, _, chain) = BuildChain(new ConstantSource(44100, 2, 0.5f));

        Assert.Equal(48000, chain.WaveFormat.SampleRate);
        Assert.Equal(2, chain.WaveFormat.Channels);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, chain.WaveFormat.Encoding);

        var buffer = new float[9600];
        var read = chain.Read(buffer, 0, buffer.Length);
        Assert.True(read > 0);
    }

    [Fact]
    public void TogglesLigados_AplicamDenoiseEGate()
    {
        var (_, _, chain) = BuildChain(new ConstantSource(48000, 1, 0.5f));

        var buffer = new float[9600]; // 100 ms estéreo
        chain.Read(buffer, 0, buffer.Length);

        // Denoiser fake corta para 0.25 (acima do limiar do gate, que abre e deixa passar).
        Assert.Equal(0.25f, buffer[^1], precision: 3);
        Assert.Equal(0.25f, buffer[^2], precision: 3);
    }

    [Fact]
    public void TogglesDesligados_SinalPassaIntacto()
    {
        var (rnNoise, gate, chain) = BuildChain(new ConstantSource(48000, 1, 0.5f));
        rnNoise.Enabled = false;
        gate.Enabled = false;

        var buffer = new float[9600];
        chain.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.Equal(0.5f, sample, precision: 5));
    }

    [Fact]
    public void SoGate_SilenciaRuidoBaixo()
    {
        var (rnNoise, _, chain) = BuildChain(new ConstantSource(48000, 1, 0.001f));
        rnNoise.Enabled = false;

        var buffer = new float[9600];
        chain.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.True(Math.Abs(sample) < 1e-4f));
    }
}
