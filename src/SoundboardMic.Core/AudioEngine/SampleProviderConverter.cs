using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Converte qualquer ISampleProvider para o formato do mixer (sample rate e canais),
/// inserindo resampler e conversão de canais apenas quando necessário.
/// </summary>
public static class SampleProviderConverter
{
    public static ISampleProvider ConvertToFormat(ISampleProvider source, WaveFormat target)
    {
        var result = source;

        if (result.WaveFormat.SampleRate != target.SampleRate)
            result = new WdlResamplingSampleProvider(result, target.SampleRate);

        if (result.WaveFormat.Channels != target.Channels)
        {
            result = (result.WaveFormat.Channels, target.Channels) switch
            {
                (1, 2) => new MonoToStereoSampleProvider(result),
                (2, 1) => new StereoToMonoSampleProvider(result),
                // Layouts incomuns (ex.: mic array 4ch): mapeia round-robin.
                _ => new MultiplexingSampleProvider(new[] { result }, target.Channels),
            };
        }

        return result;
    }
}
