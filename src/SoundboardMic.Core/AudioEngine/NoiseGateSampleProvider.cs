using NAudio.Wave;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Noise gate: silencia o sinal quando o nível fica abaixo do limiar, com
/// abertura rápida (attack), sustentação entre sílabas (hold) e fechamento
/// suave (release) para evitar cliques.
///
/// Pensado para o ramo mono do microfone; funciona por sample, então em
/// sinais multicanal o envelope é compartilhado entre os canais intercalados.
/// Os parâmetros têm defaults sensatos e não são expostos na UI.
/// </summary>
public class NoiseGateSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _thresholdLinear;
    private readonly float _attackCoef;
    private readonly float _releaseCoef;
    private readonly int _holdSamples;

    private float _gain;
    private int _holdRemaining;
    private volatile bool _enabled = true;

    public NoiseGateSampleProvider(
        ISampleProvider source,
        float thresholdDb = -45f,
        float attackMs = 5f,
        float holdMs = 200f,
        float releaseMs = 150f)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _thresholdLinear = (float)Math.Pow(10, thresholdDb / 20.0);

        var sampleRate = source.WaveFormat.SampleRate;
        _attackCoef = SmoothingCoef(attackMs, sampleRate);
        _releaseCoef = SmoothingCoef(releaseMs, sampleRate);
        _holdSamples = (int)(holdMs * sampleRate / 1000f);
    }

    /// <summary>
    /// Quando false, o provider vira pass-through puro e o envelope interno
    /// é zerado para reabrir limpo ao religar.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            if (!value)
            {
                _gain = 0f;
                _holdRemaining = 0;
            }
        }
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (!_enabled)
            return read;

        for (var i = 0; i < read; i++)
        {
            var sample = buffer[offset + i];

            float target;
            if (Math.Abs(sample) >= _thresholdLinear)
            {
                target = 1f;
                _holdRemaining = _holdSamples;
            }
            else if (_holdRemaining > 0)
            {
                target = 1f;
                _holdRemaining--;
            }
            else
            {
                target = 0f;
            }

            var coef = target > _gain ? _attackCoef : _releaseCoef;
            _gain += (target - _gain) * coef;
            buffer[offset + i] = sample * _gain;
        }

        return read;
    }

    /// <summary>Coeficiente de suavização exponencial para uma constante de tempo em ms.</summary>
    private static float SmoothingCoef(float ms, int sampleRate)
    {
        if (ms <= 0f)
            return 1f;
        return 1f - (float)Math.Exp(-1.0 / (ms / 1000.0 * sampleRate));
    }
}
