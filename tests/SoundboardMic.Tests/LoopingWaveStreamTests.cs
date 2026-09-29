using NAudio.Wave;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

public class LoopingWaveStreamTests
{
    [Fact]
    public void Read_RepeatsAcrossMultipleBoundariesAndCalls_WithOffset()
    {
        using var source = new RawSourceWaveStream(new MemoryStream(new byte[] { 1, 2, 3, 4 }),
            new WaveFormat(48000, 16, 1));
        using var loop = new LoopingWaveStream(source);
        var buffer = new byte[14];
        Assert.Equal(10, loop.Read(buffer, 2, 10));
        Assert.Equal(new byte[] { 0, 0, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 0, 0 }, buffer);
        Assert.Equal(4, loop.Read(buffer, 0, 4));
        Assert.Equal(new byte[] { 3, 4, 1, 2 }, buffer.Take(4));
    }

    [Fact]
    public void EmptyStream_EndsWithoutSpinning()
    {
        using var source = new RawSourceWaveStream(new MemoryStream(), new WaveFormat(48000, 16, 1));
        using var loop = new LoopingWaveStream(source);
        Assert.Equal(0, loop.Read(new byte[16], 0, 16));
    }

    [Theory]
    [InlineData(44100, 1)]
    [InlineData(48000, 2)]
    public void DecodedAudio_LoopsThroughResamplingAndChannelConversion(int sampleRate, int channels)
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundboard-loop-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels)))
                writer.WriteSamples(Enumerable.Repeat(0.25f, sampleRate / 20 * channels).ToArray(),
                    0, sampleRate / 20 * channels);

            using (var loop = new LoopingWaveStream(AudioFileDecoder.OpenRead(path)))
            {
                var chain = SampleProviderConverter.ConvertToFormat(AudioFileDecoder.ToSampleProvider(loop),
                    WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
                var samples = new float[9600];
                for (var i = 0; i < 5; i++)
                {
                    Assert.Equal(samples.Length, chain.Read(samples, 0, samples.Length));
                    // Ignore the resampler's initial filter transient, not any later loop boundaries.
                    Assert.All(samples.Skip(200), sample => Assert.InRange(sample, 0.24f, 0.26f));
                }
            }
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }
}
