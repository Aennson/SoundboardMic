using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Grafo de áudio:
///
///   mic físico (WasapiCapture) → buffer → 48 kHz mono → rnnoise → gate → estéreo → volume ─┐
///                                                                                          ├─ mixer principal → CABLE Input (WasapiOut)
///   sons do soundboard → resample/canais → volume ─ mixer de sons ─────────────────────────┘
///                                        └────────→ mixer de monitor → fones (WasapiOut, opcional)
///
/// Formato interno do mix: float 32-bit, 48 kHz, estéreo. Entradas com formato
/// diferente passam por WdlResampler + conversão de canais. O ramo do mic é
/// processado em mono (exigência do RNNoise) e expandido para estéreo no fim.
/// </summary>
public class MicInjectionEngine : IMicInjectionEngine
{
    private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    private static readonly WaveFormat MonoMixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

    // Buffers curtos para latência baixa em modo shared.
    private const int OutputLatencyMs = 50;
    private const int CaptureBufferMs = 50;
    private const int MicBufferDurationMs = 500;

    private readonly object _lock = new();

    // Captura do mic
    private WasapiCapture? _capture;
    private MMDevice? _captureDevice;
    private BufferedWaveProvider? _micBuffer;
    private VolumeSampleProvider? _micVolumeProvider;

    // Supressão de ruído do mic (rnnoise + gate, toggles por bypass)
    private IRnNoiseProcessor? _rnNoiseProcessor;
    private RnNoiseSampleProvider? _rnNoiseProvider;
    private NoiseGateSampleProvider? _noiseGateProvider;
    private bool _noiseSuppressionEnabled;
    private bool _noiseGateEnabled;
    private bool _noiseSuppressionAvailable = true;
    private Exception? _rnNoiseLoadError;

    // Mix principal → dispositivo virtual
    private MixingSampleProvider? _soundMixer;
    private VolumeSampleProvider? _soundboardVolumeProvider;
    private MixingSampleProvider? _mainMixer;
    private LimiterSampleProvider? _mainLimiter;
    private WasapiOut? _output;
    private MMDevice? _outputDevice;

    // Monitoramento local
    private MixingSampleProvider? _monitorMixer;
    private VolumeSampleProvider? _monitorVolumeProvider;
    private LimiterSampleProvider? _monitorLimiter;
    private WasapiOut? _monitorOutput;
    private MMDevice? _monitorDevice;
    private string? _monitorDeviceId;
    private bool _monitorEnabled;

    // Segunda saída virtual: réplica independente do mix principal (mic + sons),
    // com buffer de mic e cadeia de ruído próprios para não competir pelo mesmo leitor.
    private BufferedWaveProvider? _secondaryMicBuffer;
    private IRnNoiseProcessor? _secondaryRnNoiseProcessor;
    private RnNoiseSampleProvider? _secondaryRnNoiseProvider;
    private NoiseGateSampleProvider? _secondaryNoiseGateProvider;
    private VolumeSampleProvider? _secondaryMicVolumeProvider;
    private MixingSampleProvider? _secondarySoundMixer;
    private VolumeSampleProvider? _secondarySoundVolumeProvider;
    private MixingSampleProvider? _secondaryMainMixer;
    private LimiterSampleProvider? _secondaryLimiter;
    private WasapiOut? _secondaryOutput;
    private MMDevice? _secondaryDevice;
    private string? _secondaryDeviceId;
    private bool _secondaryEnabled;

    private float _micVolume = 1.0f;
    private float _soundboardVolume = 1.0f;
    private bool _isRunning;
    private bool _stopping;

    private sealed record ActiveSound(
        ISampleProvider Input, WaveStream Reader, MixingSampleProvider Owner, string FilePath, bool Loop);

    private readonly List<ActiveSound> _activeSounds = new();

    public bool IsRunning
    {
        get { lock (_lock) return _isRunning; }
    }

    public int ActiveSoundCount
    {
        get
        {
            // Sons do monitor duplicam o mesmo disparo; conta só os do mix principal.
            lock (_lock)
                return _activeSounds.Count(s => ReferenceEquals(s.Owner, _soundMixer));
        }
    }

    public IReadOnlyList<string> GetActiveSoundPaths()
    {
        // Sons do monitor duplicam o mesmo disparo; considera só os do mix principal.
        lock (_lock)
            return _activeSounds
                .Where(s => ReferenceEquals(s.Owner, _soundMixer))
                .Select(s => s.FilePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    public float MicVolume
    {
        get { lock (_lock) return _micVolume; }
        set
        {
            lock (_lock)
            {
                _micVolume = Math.Clamp(value, 0f, 2f);
                if (_micVolumeProvider is not null)
                    _micVolumeProvider.Volume = VolumeCurve.ToGain(_micVolume);
                if (_secondaryMicVolumeProvider is not null)
                    _secondaryMicVolumeProvider.Volume = VolumeCurve.ToGain(_micVolume);
            }
        }
    }

    public IReadOnlyList<string> GetLoopingSoundPaths()
    {
        lock (_lock)
            return _activeSounds
                .Where(s => s.Loop && ReferenceEquals(s.Owner, _soundMixer))
                .Select(s => s.FilePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    public float SoundboardVolume
    {
        get { lock (_lock) return _soundboardVolume; }
        set
        {
            lock (_lock)
            {
                _soundboardVolume = Math.Clamp(value, 0f, 2f);
                if (_soundboardVolumeProvider is not null)
                    _soundboardVolumeProvider.Volume = VolumeCurve.ToGain(_soundboardVolume);
                if (_monitorVolumeProvider is not null)
                    _monitorVolumeProvider.Volume = VolumeCurve.ToGain(_soundboardVolume);
                if (_secondarySoundVolumeProvider is not null)
                    _secondarySoundVolumeProvider.Volume = VolumeCurve.ToGain(_soundboardVolume);
            }
        }
    }

    public bool MonitorEnabled
    {
        get { lock (_lock) return _monitorEnabled; }
        set
        {
            lock (_lock)
            {
                if (_monitorEnabled == value)
                    return;
                _monitorEnabled = value;
                if (!_isRunning)
                    return;
                if (value)
                    StartMonitorCore();
                else
                    StopMonitorCore();
            }
        }
    }

    public bool SecondaryOutputEnabled
    {
        get { lock (_lock) return _secondaryEnabled; }
        set
        {
            lock (_lock)
            {
                if (_secondaryEnabled == value)
                    return;
                _secondaryEnabled = value;
                if (!_isRunning)
                    return;
                if (value)
                    StartSecondaryCore();
                else
                    StopSecondaryCore();
            }
        }
    }

    public bool NoiseSuppressionEnabled
    {
        get { lock (_lock) return _noiseSuppressionEnabled; }
        set
        {
            lock (_lock)
            {
                _noiseSuppressionEnabled = value;
                if (_rnNoiseProvider is not null)
                    _rnNoiseProvider.Enabled = value;
                if (_secondaryRnNoiseProvider is not null)
                    _secondaryRnNoiseProvider.Enabled = value;
            }
        }
    }

    public bool NoiseGateEnabled
    {
        get { lock (_lock) return _noiseGateEnabled; }
        set
        {
            lock (_lock)
            {
                _noiseGateEnabled = value;
                if (_noiseGateProvider is not null)
                    _noiseGateProvider.Enabled = value;
                if (_secondaryNoiseGateProvider is not null)
                    _secondaryNoiseGateProvider.Enabled = value;
            }
        }
    }

    public bool NoiseSuppressionAvailable
    {
        get { lock (_lock) return _noiseSuppressionAvailable; }
    }

    /// <summary>Erro de load da lib nativa do rnnoise no último Start, se houve.</summary>
    public Exception? NoiseSuppressionLoadError
    {
        get { lock (_lock) return _rnNoiseLoadError; }
    }

    public event EventHandler? ActiveSoundsChanged;
    public event EventHandler<EngineStoppedEventArgs>? StoppedUnexpectedly;

    public void Start(MicInjectionOptions options)
    {
        lock (_lock)
        {
            if (_isRunning)
                throw new InvalidOperationException("O motor de injeção já está rodando.");

            _micVolume = Math.Clamp(options.MicVolume, 0f, 2f);
            _soundboardVolume = Math.Clamp(options.SoundboardVolume, 0f, 2f);
            _monitorDeviceId = options.MonitorDeviceId;
            _monitorEnabled = options.MonitorEnabled;
            _secondaryDeviceId = options.SecondaryOutputDeviceId;
            _secondaryEnabled = options.SecondaryOutputEnabled;
            _noiseSuppressionEnabled = options.NoiseSuppressionEnabled;
            _noiseGateEnabled = options.NoiseGateEnabled;

            try
            {
                // 1. Captura do microfone físico.
                _captureDevice = AudioDeviceService.ResolveDevice(options.MicDeviceId, DataFlow.Capture);
                _capture = new WasapiCapture(_captureDevice, useEventSync: true, CaptureBufferMs);
                _micBuffer = new BufferedWaveProvider(_capture.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromMilliseconds(MicBufferDurationMs),
                    DiscardOnBufferOverflow = true, // nunca travar a captura
                };
                _capture.DataAvailable += OnMicDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                // Ramo do mic em mono 48 kHz: rnnoise (se a lib nativa carregar)
                // seguido do noise gate, ambos sempre na cadeia com bypass.
                ISampleProvider micChain = SampleProviderConverter.ConvertToFormat(
                    _micBuffer.ToSampleProvider(), MonoMixFormat);

                if (RnNoiseProcessor.TryCreate(out _rnNoiseProcessor, out _rnNoiseLoadError))
                {
                    _rnNoiseProvider = new RnNoiseSampleProvider(micChain, _rnNoiseProcessor!)
                    {
                        Enabled = _noiseSuppressionEnabled,
                    };
                    micChain = _rnNoiseProvider;
                    _noiseSuppressionAvailable = true;
                }
                else
                {
                    _noiseSuppressionAvailable = false;
                }

                _noiseGateProvider = new NoiseGateSampleProvider(micChain)
                {
                    Enabled = _noiseGateEnabled,
                };
                var micStereo = new MonoToStereoSampleProvider(_noiseGateProvider);
                _micVolumeProvider = new VolumeSampleProvider(micStereo) { Volume = VolumeCurve.ToGain(_micVolume) };

                // 2. Sub-mixer só dos sons do soundboard (permite pânico sem tocar no mic).
                _soundMixer = new MixingSampleProvider(MixFormat) { ReadFully = true };
                _soundMixer.MixerInputEnded += OnMixerInputEnded;
                _soundboardVolumeProvider = new VolumeSampleProvider(_soundMixer)
                {
                    Volume = VolumeCurve.ToGain(_soundboardVolume),
                };

                // 3. Mixer principal: mic + soundboard → dispositivo virtual.
                _mainMixer = new MixingSampleProvider(MixFormat) { ReadFully = true };
                _mainMixer.AddMixerInput(_micVolumeProvider);
                _mainMixer.AddMixerInput((ISampleProvider)_soundboardVolumeProvider);

                // Limiter final: evita que a soma dos ganhos (volume do mic + volume
                // individual do som × volume mestre, cada um até 2.0x) estoure a
                // amplitude e chegue distorcido/muito mais alto para quem escuta.
                _mainLimiter = new LimiterSampleProvider(_mainMixer);

                _outputDevice = AudioDeviceService.ResolveDevice(options.OutputDeviceId, DataFlow.Render);
                _output = new WasapiOut(_outputDevice, AudioClientShareMode.Shared,
                    useEventSync: true, OutputLatencyMs);
                _output.PlaybackStopped += OnPlaybackStopped;
                _output.Init(_mainLimiter);

                _output.Play();
                _capture.StartRecording();
                _isRunning = true;

                // 4. Monitoramento local opcional.
                if (_monitorEnabled)
                    StartMonitorCore();

                // 5. Segunda saída virtual opcional (mesmo mix em outro dispositivo).
                if (_secondaryEnabled)
                    StartSecondaryCore();
            }
            catch
            {
                StopCore();
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
            StopCore();
        ActiveSoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PlaySound(string filePath, float volume = 1.0f, bool loop = false)
    {
        lock (_lock)
        {
            if (!_isRunning || _soundMixer is null)
                throw new InvalidOperationException(
                    "O motor de injeção não está rodando; inicie-o antes de disparar sons.");

            AddSoundTo(_soundMixer, filePath, volume, loop);

            // Monitor usa um reader próprio: o mesmo stream não pode alimentar
            // dois outputs em ritmos diferentes.
            if (_monitorEnabled && _monitorMixer is not null)
                AddSoundTo(_monitorMixer, filePath, volume, loop);

            // Mesma regra para a segunda saída virtual.
            if (_secondarySoundMixer is not null)
                AddSoundTo(_secondarySoundMixer, filePath, volume, loop);
        }
        ActiveSoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StopAllSounds()
    {
        lock (_lock)
        {
            if (_activeSounds.Count == 0)
                return;

            foreach (var sound in _activeSounds)
            {
                sound.Owner.RemoveMixerInput(sound.Input);
                sound.Reader.Dispose();
            }
            _activeSounds.Clear();
        }
        ActiveSoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StopSound(string filePath) => StopSound(filePath, loopsOnly: false);

    public void StopLoopSound(string filePath) => StopSound(filePath, loopsOnly: true);

    private void StopSound(string filePath, bool loopsOnly)
    {
        lock (_lock)
        {
            var matches = _activeSounds.Where(s =>
                (!loopsOnly || s.Loop) &&
                string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
                return;

            foreach (var sound in matches)
            {
                sound.Owner.RemoveMixerInput(sound.Input);
                sound.Reader.Dispose();
                _activeSounds.Remove(sound);
            }
        }
        ActiveSoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_lock)
            StopCore();
        GC.SuppressFinalize(this);
    }

    // ---- internos (sempre chamados sob _lock) ----

    private void AddSoundTo(MixingSampleProvider mixer, string filePath, float volume, bool loop)
    {
        var reader = AudioFileDecoder.OpenRead(filePath);
        try
        {
            if (loop)
                reader = new LoopingWaveStream(reader);
            var chain = SampleProviderConverter.ConvertToFormat(
                AudioFileDecoder.ToSampleProvider(reader), MixFormat);
            var input = new VolumeSampleProvider(chain) { Volume = VolumeCurve.ToGain(Math.Clamp(volume, 0f, 2f)) };

            _activeSounds.Add(new ActiveSound(input, reader, mixer, filePath, loop));
            mixer.AddMixerInput((ISampleProvider)input);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private void StartMonitorCore()
    {
        if (_monitorOutput is not null)
            return;

        _monitorMixer = new MixingSampleProvider(MixFormat) { ReadFully = true };
        _monitorMixer.MixerInputEnded += OnMixerInputEnded;
        _monitorVolumeProvider = new VolumeSampleProvider(_monitorMixer)
        {
            Volume = VolumeCurve.ToGain(_soundboardVolume),
        };

        _monitorDevice = AudioDeviceService.ResolveDevice(_monitorDeviceId, DataFlow.Render);
        _monitorOutput = new WasapiOut(_monitorDevice, AudioClientShareMode.Shared,
            useEventSync: true, OutputLatencyMs);
        _monitorLimiter = new LimiterSampleProvider(_monitorVolumeProvider);
        _monitorOutput.Init(_monitorLimiter);
        _monitorOutput.Play();
    }

    private void StopMonitorCore()
    {
        if (_monitorMixer is not null)
            _monitorMixer.MixerInputEnded -= OnMixerInputEnded;

        _monitorOutput?.Dispose();
        _monitorOutput = null;
        _monitorDevice?.Dispose();
        _monitorDevice = null;

        // Descarta sons pendentes do monitor.
        for (var i = _activeSounds.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_activeSounds[i].Owner, _monitorMixer))
            {
                _activeSounds[i].Reader.Dispose();
                _activeSounds.RemoveAt(i);
            }
        }
        _monitorMixer = null;
        _monitorVolumeProvider = null;
        _monitorLimiter = null;
    }

    /// <summary>
    /// Sobe a segunda saída virtual: uma réplica completa do mix principal
    /// (mic + sons) com buffer e leitores próprios, já que um mesmo provider
    /// não pode alimentar dois dispositivos em ritmos diferentes.
    /// </summary>
    private void StartSecondaryCore()
    {
        if (_secondaryOutput is not null || _capture is null)
            return;

        var device = AudioDeviceService.ResolveDevice(_secondaryDeviceId, DataFlow.Render);

        // Mesmo dispositivo da saída principal duplicaria o áudio: ignora.
        if (_outputDevice is not null && string.Equals(device.ID, _outputDevice.ID, StringComparison.OrdinalIgnoreCase))
        {
            device.Dispose();
            return;
        }

        try
        {
            _secondaryDevice = device;
            _secondaryMicBuffer = new BufferedWaveProvider(_capture.WaveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(MicBufferDurationMs),
                DiscardOnBufferOverflow = true,
            };

            ISampleProvider micChain = SampleProviderConverter.ConvertToFormat(
                _secondaryMicBuffer.ToSampleProvider(), MonoMixFormat);

            if (_noiseSuppressionAvailable
                && RnNoiseProcessor.TryCreate(out _secondaryRnNoiseProcessor, out _))
            {
                _secondaryRnNoiseProvider = new RnNoiseSampleProvider(micChain, _secondaryRnNoiseProcessor!)
                {
                    Enabled = _noiseSuppressionEnabled,
                };
                micChain = _secondaryRnNoiseProvider;
            }

            _secondaryNoiseGateProvider = new NoiseGateSampleProvider(micChain)
            {
                Enabled = _noiseGateEnabled,
            };
            var micStereo = new MonoToStereoSampleProvider(_secondaryNoiseGateProvider);
            _secondaryMicVolumeProvider = new VolumeSampleProvider(micStereo)
            {
                Volume = VolumeCurve.ToGain(_micVolume),
            };

            _secondarySoundMixer = new MixingSampleProvider(MixFormat) { ReadFully = true };
            _secondarySoundMixer.MixerInputEnded += OnMixerInputEnded;
            _secondarySoundVolumeProvider = new VolumeSampleProvider(_secondarySoundMixer)
            {
                Volume = VolumeCurve.ToGain(_soundboardVolume),
            };

            _secondaryMainMixer = new MixingSampleProvider(MixFormat) { ReadFully = true };
            _secondaryMainMixer.AddMixerInput(_secondaryMicVolumeProvider);
            _secondaryMainMixer.AddMixerInput(_secondarySoundVolumeProvider);

            _secondaryLimiter = new LimiterSampleProvider(_secondaryMainMixer);
            _secondaryOutput = new WasapiOut(_secondaryDevice, AudioClientShareMode.Shared,
                useEventSync: true, OutputLatencyMs);
            _secondaryOutput.Init(_secondaryLimiter);
            _secondaryOutput.Play();
        }
        catch
        {
            StopSecondaryCore();
            throw;
        }
    }

    private void StopSecondaryCore()
    {
        if (_secondarySoundMixer is not null)
            _secondarySoundMixer.MixerInputEnded -= OnMixerInputEnded;

        _secondaryOutput?.Dispose();
        _secondaryOutput = null;
        _secondaryDevice?.Dispose();
        _secondaryDevice = null;

        // Descarta sons pendentes da segunda saída.
        for (var i = _activeSounds.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_activeSounds[i].Owner, _secondarySoundMixer))
            {
                _secondarySoundMixer?.RemoveMixerInput(_activeSounds[i].Input);
                _activeSounds[i].Reader.Dispose();
                _activeSounds.RemoveAt(i);
            }
        }

        _secondaryRnNoiseProcessor?.Dispose();
        _secondaryRnNoiseProcessor = null;
        _secondaryRnNoiseProvider = null;
        _secondaryNoiseGateProvider = null;
        _secondaryMicBuffer = null;
        _secondaryMicVolumeProvider = null;
        _secondarySoundMixer = null;
        _secondarySoundVolumeProvider = null;
        _secondaryMainMixer = null;
        _secondaryLimiter = null;
    }

    private void StopCore()
    {
        _stopping = true;
        try
        {
            if (_capture is not null)
            {
                _capture.DataAvailable -= OnMicDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                try { _capture.StopRecording(); } catch { /* já parado */ }
                _capture.Dispose();
                _capture = null;
            }
            _captureDevice?.Dispose();
            _captureDevice = null;

            if (_output is not null)
            {
                _output.PlaybackStopped -= OnPlaybackStopped;
                _output.Dispose();
                _output = null;
            }
            _outputDevice?.Dispose();
            _outputDevice = null;

            StopMonitorCore();
            StopSecondaryCore();

            foreach (var sound in _activeSounds)
                sound.Reader.Dispose();
            _activeSounds.Clear();

            _rnNoiseProcessor?.Dispose();
            _rnNoiseProcessor = null;
            _rnNoiseProvider = null;
            _noiseGateProvider = null;

            _micBuffer = null;
            _micVolumeProvider = null;
            _soundMixer = null;
            _soundboardVolumeProvider = null;
            _mainMixer = null;
            _mainLimiter = null;
            _isRunning = false;
        }
        finally
        {
            _stopping = false;
        }
    }

    private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Thread de captura do WASAPI: só enfileira os bytes.
        _micBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        _secondaryMicBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnMixerInputEnded(object? sender, SampleProviderEventArgs e)
    {
        lock (_lock)
        {
            var index = _activeSounds.FindIndex(s => ReferenceEquals(s.Input, e.SampleProvider));
            if (index < 0)
                return;
            _activeSounds[index].Reader.Dispose();
            _activeSounds.RemoveAt(index);
        }
        ActiveSoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        => HandleUnexpectedStop(e.Exception);

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        => HandleUnexpectedStop(e.Exception);

    private void HandleUnexpectedStop(Exception? exception)
    {
        lock (_lock)
        {
            if (!_isRunning || _stopping)
                return; // parada intencional via Stop()
            StopCore();
        }
        StoppedUnexpectedly?.Invoke(this, new EngineStoppedEventArgs { Exception = exception });
    }
}
