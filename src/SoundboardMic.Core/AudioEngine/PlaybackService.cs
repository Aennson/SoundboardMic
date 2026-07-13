using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundboardMic.Core.AudioEngine;

public class PlaybackService : IPlaybackService
{
    // Latência do WASAPI shared para preview; valor conservador, não crítico aqui.
    private const int LatencyMs = 100;

    private readonly object _lock = new();
    private WasapiOut? _output;
    private WaveStream? _reader;
    private MMDevice? _device;

    public bool IsPlaying
    {
        get
        {
            lock (_lock)
                return _output?.PlaybackState == PlaybackState.Playing;
        }
    }

    public event EventHandler? PlaybackStopped;

    public void Play(string filePath, string? deviceId = null, float volume = 1.0f)
    {
        var reader = AudioFileDecoder.OpenRead(filePath);
        var sampleProvider = new VolumeSampleProvider(AudioFileDecoder.ToSampleProvider(reader))
        {
            Volume = Math.Clamp(volume, 0f, 2f),
        };

        MMDevice? device = null;
        WasapiOut? output = null;
        try
        {
            device = AudioDeviceService.ResolveDevice(deviceId, DataFlow.Render);
            output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, LatencyMs);
            output.Init(sampleProvider);
        }
        catch
        {
            output?.Dispose();
            device?.Dispose();
            reader.Dispose();
            throw;
        }

        lock (_lock)
        {
            StopCore();
            _reader = reader;
            _device = device;
            _output = output;
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Play();
        }
    }

    public void Stop()
    {
        lock (_lock)
            StopCore();
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            // Só limpa se o evento veio da saída atual (Play pode ter trocado).
            if (!ReferenceEquals(sender, _output))
                return;
            StopCore();
        }
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void StopCore()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }
        _reader?.Dispose();
        _reader = null;
        _device?.Dispose();
        _device = null;
    }

    public void Dispose()
    {
        lock (_lock)
            StopCore();
        GC.SuppressFinalize(this);
    }
}
