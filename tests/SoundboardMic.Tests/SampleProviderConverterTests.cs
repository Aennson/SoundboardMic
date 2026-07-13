using NAudio.Wave;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

public class SampleProviderConverterTests
{
    private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    /// <summary>Fonte de teste: gera um valor constante no formato indicado.</summary>
    private sealed class ConstantSource : ISampleProvider
    {
        private readonly float _value;

        public ConstantSource(int sampleRate, int channels, float value = 0.5f)
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

    [Theory]
    [InlineData(44100, 1)] // mp3 mono típico
    [InlineData(44100, 2)] // CD estéreo
    [InlineData(48000, 1)] // mic mono em 48k
    [InlineData(22050, 2)] // arquivo antigo de baixa qualidade
    [InlineData(48000, 4)] // mic array 4 canais
    public void ConvertToFormat_ProduzFormatoDoMixer(int sampleRate, int channels)
    {
        var source = new ConstantSource(sampleRate, channels);

        var converted = SampleProviderConverter.ConvertToFormat(source, MixFormat);

        Assert.Equal(MixFormat.SampleRate, converted.WaveFormat.SampleRate);
        Assert.Equal(MixFormat.Channels, converted.WaveFormat.Channels);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, converted.WaveFormat.Encoding);

        // O pipeline convertido precisa ser legível de fato.
        var buffer = new float[MixFormat.SampleRate / 10 * MixFormat.Channels]; // 100 ms
        var read = converted.Read(buffer, 0, buffer.Length);
        Assert.True(read > 0);
    }

    [Fact]
    public void ConvertToFormat_FormatoIgual_NaoEmbrulha()
    {
        var source = new ConstantSource(48000, 2);

        var converted = SampleProviderConverter.ConvertToFormat(source, MixFormat);

        Assert.Same(source, converted);
    }

    [Fact]
    public void ConvertToFormat_MonoParaEstereo_DuplicaOSinal()
    {
        var source = new ConstantSource(48000, 1, value: 0.25f);

        var converted = SampleProviderConverter.ConvertToFormat(source, MixFormat);

        var buffer = new float[64];
        converted.Read(buffer, 0, buffer.Length);

        // Ambos os canais devem carregar o sinal original.
        Assert.All(buffer, sample => Assert.Equal(0.25f, sample, precision: 3));
    }

    [Fact]
    public void ConvertToFormat_ResampleMantemAmplitude()
    {
        var source = new ConstantSource(44100, 2, value: 0.5f);

        var converted = SampleProviderConverter.ConvertToFormat(source, MixFormat);

        var buffer = new float[4800]; // 50 ms em 48k estéreo
        converted.Read(buffer, 0, buffer.Length);

        // Sinal constante resampleado continua ~constante (ignora bordas do filtro).
        var meio = buffer.Skip(1000).Take(2000).ToArray();
        Assert.All(meio, sample => Assert.Equal(0.5f, sample, precision: 2));
    }
}
