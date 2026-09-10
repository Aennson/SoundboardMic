using NAudio.Wave;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Limiter de pico (brickwall suave) aplicado no estágio final do mix, depois
/// de somados mic + sons do soundboard. Sem isso, ganhos individuais (volume
/// do som × volume mestre, cada um até 2.0x) podem somar mais de 1.0 de
/// amplitude e estourar (clipping) na saída, chegando muito mais alto/
/// distorcido do que os sliders sugerem.
///
/// Usa detecção de pico com release suave (não é um limiter "brickwall"
/// perfeito, mas evita cliques abruptos), reduzindo o ganho da cadeia inteira
/// quando o pico ultrapassa o teto configurado.
/// </summary>
public class LimiterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _ceiling;
    private readonly float _releaseCoef;

    private float _gain = 1f;

    public LimiterSampleProvider(ISampleProvider source, float ceiling = 0.98f, float releaseMs = 100f)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _ceiling = ceiling;

        var sampleRate = source.WaveFormat.SampleRate;
        _releaseCoef = 1f - (float)Math.Exp(-1.0 / (releaseMs / 1000.0 * sampleRate));
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);

        for (var i = 0; i < read; i++)
        {
            var sample = buffer[offset + i];
            var peak = Math.Abs(sample);

            // Reduz o ganho imediatamente se o pico (já com o ganho atual) estourar o teto;
            // solta (recupera para 1.0) suavemente para não gerar "bombeamento" perceptível.
            if (peak * _gain > _ceiling)
            {
                _gain = _ceiling / peak;
            }
            else if (_gain < 1f)
            {
                _gain += (1f - _gain) * _releaseCoef;
            }

            buffer[offset + i] = Math.Clamp(sample * _gain, -1f, 1f);
        }

        return read;
    }
}
