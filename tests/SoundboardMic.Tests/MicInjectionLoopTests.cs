using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

public class MicInjectionLoopTests
{
    [Fact]
    public void Loop_ReachesBothMixers_AndAllStopPathsReleaseReaders()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundboard-engine-loop-{Guid.NewGuid():N}.wav");
        try
        {
            var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
            using (var writer = new WaveFileWriter(path, format))
                writer.WriteSamples(Enumerable.Repeat(0.25f, 4800).ToArray(), 0, 4800);

            using var engine = new MicInjectionEngine();
            var main = new MixingSampleProvider(format);
            var monitor = new MixingSampleProvider(format);
            // Replace only WASAPI setup: exercise the real playback chains without using physical devices.
            SetField(engine, "_soundMixer", main);
            SetField(engine, "_monitorMixer", monitor);
            SetField(engine, "_monitorEnabled", true);
            SetField(engine, "_isRunning", true);

            engine.PlaySound(path, loop: true);
            Assert.Equal(1, engine.ActiveSoundCount);
            Assert.Equal(path, Assert.Single(engine.GetLoopingSoundPaths()));
            foreach (var mixer in new[] { main, monitor })
            {
                var samples = new float[48000];
                Assert.Equal(samples.Length, mixer.Read(samples, 0, samples.Length));
                Assert.All(samples, value => Assert.Equal(0.25f, value));
            }

            engine.PlaySound(path);
            engine.StopLoopSound(path.ToUpperInvariant());
            Assert.Empty(engine.GetLoopingSoundPaths());
            Assert.Equal(1, engine.ActiveSoundCount);
            Assert.Single(main.MixerInputs);
            Assert.Single(monitor.MixerInputs);

            engine.PlaySound(path, loop: true);
            engine.StopSound(path);
            Assert.Empty(main.MixerInputs);
            Assert.Empty(monitor.MixerInputs);
            Assert.Empty(engine.GetActiveSoundPaths());

            engine.PlaySound(path, loop: true);
            engine.StopAllSounds();
            Assert.Empty(main.MixerInputs);
            Assert.Empty(monitor.MixerInputs);
            Assert.Empty(engine.GetLoopingSoundPaths());

            engine.PlaySound(path, loop: true);
            engine.Stop();
            Assert.False(engine.IsRunning);
            Assert.Empty(engine.GetLoopingSoundPaths());
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SecondaryOutput_ReceivesSounds_AndDisablingReleasesThem()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundboard-engine-secondary-{Guid.NewGuid():N}.wav");
        try
        {
            var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
            using (var writer = new WaveFileWriter(path, format))
                writer.WriteSamples(Enumerable.Repeat(0.25f, 4800).ToArray(), 0, 4800);

            using var engine = new MicInjectionEngine();
            var main = new MixingSampleProvider(format);
            var secondary = new MixingSampleProvider(format);
            SetField(engine, "_soundMixer", main);
            SetField(engine, "_secondarySoundMixer", secondary);
            SetField(engine, "_secondaryEnabled", true);
            SetField(engine, "_isRunning", true);

            engine.PlaySound(path, loop: true);

            // Cada saída tem leitor próprio: as duas reproduzem o mesmo som,
            // mas a contagem pública ignora as duplicatas.
            Assert.Equal(1, engine.ActiveSoundCount);
            Assert.Single(main.MixerInputs);
            Assert.Single(secondary.MixerInputs);
            foreach (var mixer in new[] { main, secondary })
            {
                var samples = new float[48000];
                Assert.Equal(samples.Length, mixer.Read(samples, 0, samples.Length));
                Assert.All(samples, value => Assert.Equal(0.25f, value));
            }

            // Desligar a segunda saída descarta só os leitores dela.
            engine.SecondaryOutputEnabled = false;
            Assert.Empty(secondary.MixerInputs);
            Assert.Single(main.MixerInputs);
            Assert.Equal(1, engine.ActiveSoundCount);

            engine.PlaySound(path);
            Assert.Empty(secondary.MixerInputs);

            engine.Stop();
            Assert.False(engine.IsRunning);
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }

    private static void SetField(MicInjectionEngine engine, string name, object value)
    {
        var field = typeof(MicInjectionEngine).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field.SetValue(engine, value);
    }
}
