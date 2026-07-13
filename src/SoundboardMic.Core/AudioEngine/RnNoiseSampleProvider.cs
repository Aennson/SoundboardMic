using NAudio.Wave;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Aplica supressão de ruído RNNoise ao sinal. Exige fonte mono 48 kHz float
/// (formato nativo do denoiser). O processamento em frames de 480 samples fica
/// a cargo do <see cref="IRnNoiseProcessor"/>.
///
/// O toggle é por bypass: com <see cref="Enabled"/> false o provider devolve o
/// sinal intacto; ao religar, o estado interno do denoiser é limpo na própria
/// thread de áudio (sem lock no hot path).
/// </summary>
public class RnNoiseSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly IRnNoiseProcessor _processor;

    private volatile bool _enabled = true;
    private volatile bool _pendingReset;

    public RnNoiseSampleProvider(ISampleProvider source, IRnNoiseProcessor processor)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));

        var format = source.WaveFormat;
        if (format.SampleRate != 48000 || format.Channels != 1 ||
            format.Encoding != WaveFormatEncoding.IeeeFloat)
        {
            throw new ArgumentException(
                $"RNNoise exige fonte mono 48 kHz IEEE float; recebido {format}.", nameof(source));
        }
    }

    /// <summary>
    /// Quando false, pass-through puro. Religar agenda um Reset do denoiser
    /// para não misturar estado acumulado antes do bypass.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            if (value)
                _pendingReset = true;
            _enabled = value;
        }
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (!_enabled || read == 0)
            return read;

        if (_pendingReset)
        {
            _processor.Reset();
            _pendingReset = false;
        }

        _processor.Process(buffer, offset, read);
        return read;
    }
}
