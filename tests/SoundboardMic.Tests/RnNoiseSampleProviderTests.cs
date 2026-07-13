using NAudio.Wave;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

public class RnNoiseSampleProviderTests
{
    /// <summary>Fake do denoiser: multiplica por 0.5 e conta as chamadas.</summary>
    private sealed class FakeRnNoiseProcessor : IRnNoiseProcessor
    {
        public int ProcessCalls { get; private set; }
        public int ResetCalls { get; private set; }

        public void Process(float[] buffer, int offset, int count)
        {
            ProcessCalls++;
            for (var i = 0; i < count; i++)
                buffer[offset + i] *= 0.5f;
        }

        public void Reset() => ResetCalls++;

        public void Dispose() { }
    }

    private sealed class ConstantSource : ISampleProvider
    {
        private readonly float _value;

        public ConstantSource(float value, int sampleRate = 48000, int channels = 1)
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

    [Fact]
    public void Enabled_ChamaProcessorEDevolveSamplesProcessados()
    {
        var fake = new FakeRnNoiseProcessor();
        var provider = new RnNoiseSampleProvider(new ConstantSource(0.4f), fake);

        var buffer = new float[480];
        var read = provider.Read(buffer, 0, buffer.Length);

        Assert.Equal(buffer.Length, read);
        Assert.Equal(1, fake.ProcessCalls);
        Assert.All(buffer, sample => Assert.Equal(0.2f, sample, precision: 5));
    }

    [Fact]
    public void Disabled_BypassNaoChamaProcessor()
    {
        var fake = new FakeRnNoiseProcessor();
        var provider = new RnNoiseSampleProvider(new ConstantSource(0.4f), fake)
        {
            Enabled = false,
        };

        var buffer = new float[480];
        provider.Read(buffer, 0, buffer.Length);

        Assert.Equal(0, fake.ProcessCalls);
        Assert.All(buffer, sample => Assert.Equal(0.4f, sample));
    }

    [Fact]
    public void Religar_ChamaResetAntesDeProcessar()
    {
        var fake = new FakeRnNoiseProcessor();
        var provider = new RnNoiseSampleProvider(new ConstantSource(0.4f), fake);

        var buffer = new float[480];
        provider.Read(buffer, 0, buffer.Length);
        Assert.Equal(0, fake.ResetCalls); // primeira leitura não precisa de reset

        provider.Enabled = false;
        provider.Read(buffer, 0, buffer.Length);

        provider.Enabled = true;
        provider.Read(buffer, 0, buffer.Length);

        Assert.Equal(1, fake.ResetCalls);
        Assert.Equal(2, fake.ProcessCalls);
    }

    [Theory]
    [InlineData(44100, 1)] // sample rate errado
    [InlineData(48000, 2)] // estéreo
    public void FonteForaDoFormato_LancaArgumentException(int sampleRate, int channels)
    {
        var source = new ConstantSource(0.4f, sampleRate, channels);

        Assert.Throws<ArgumentException>(
            () => new RnNoiseSampleProvider(source, new FakeRnNoiseProcessor()));
    }

    [Fact]
    public void PreservaWaveFormatDaFonte()
    {
        var source = new ConstantSource(0.4f);
        var provider = new RnNoiseSampleProvider(source, new FakeRnNoiseProcessor());

        Assert.Same(source.WaveFormat, provider.WaveFormat);
    }
}
