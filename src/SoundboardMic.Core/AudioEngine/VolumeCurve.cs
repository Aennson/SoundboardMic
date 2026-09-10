namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Converte o valor "bruto" de um slider de volume (0.0 a 2.0, onde 1.0 = volume
/// original) no ganho linear real aplicado ao áudio.
///
/// Ganho linear puro não corresponde à percepção humana de volume (que é
/// aproximadamente logarítmica): reduzir o slider para 50% (-6 dB) soa quase
/// tão alto quanto 100%, forçando o usuário a arrastar até perto de 10% para
/// perceber uma redução real. Para compensar isso, valores abaixo de 1.0
/// (unity) recebem uma curva quadrática (taper), que atenua com muito mais
/// força na metade inferior do curso do slider — mais parecido com um fader
/// de áudio "profissional". Acima de 1.0 (reforço/boost) o ganho continua
/// linear, preservando o comportamento existente para quem precisa aumentar
/// um som baixo.
/// </summary>
public static class VolumeCurve
{
    /// <summary>
    /// Converte o valor do slider (0.0 a 2.0) no ganho linear a aplicar no
    /// <c>VolumeSampleProvider</c>.
    /// </summary>
    public static float ToGain(float sliderValue)
    {
        var v = Math.Clamp(sliderValue, 0f, 2f);

        // Abaixo de 1.0: curva quadrática (v² mantém 0→0 e 1→1, mas atenua
        // muito mais na região intermediária, ex.: 0.5 → 0.25 de ganho).
        // Acima de 1.0: linear, igual ao comportamento anterior.
        return v <= 1f ? v * v : v;
    }
}
