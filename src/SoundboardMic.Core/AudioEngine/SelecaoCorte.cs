using System.Globalization;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Trecho contínuo [Inicio, Fim] selecionado no editor. Sempre normalizado: dentro do áudio,
/// com início antes do fim e pelo menos <see cref="AudioTrimmer.DuracaoMinima"/> de duração.
/// </summary>
public readonly record struct SelecaoCorte(TimeSpan Inicio, TimeSpan Fim)
{
    public TimeSpan Duracao => Fim - Inicio;

    public static SelecaoCorte Tudo(TimeSpan duracaoAudio) => new(TimeSpan.Zero, duracaoAudio);

    /// <summary>
    /// Ajusta o início mantendo o fim; se não couber o mínimo, o início recua.
    /// </summary>
    public SelecaoCorte ComInicio(TimeSpan inicio, TimeSpan duracaoAudio)
    {
        var minimo = MinimoPara(duracaoAudio);
        var limite = Fim - minimo;
        return new SelecaoCorte(Clamp(inicio, TimeSpan.Zero, limite < TimeSpan.Zero ? TimeSpan.Zero : limite), Fim);
    }

    /// <summary>Ajusta o fim mantendo o início; se não couber o mínimo, o fim avança.</summary>
    public SelecaoCorte ComFim(TimeSpan fim, TimeSpan duracaoAudio)
    {
        var minimo = MinimoPara(duracaoAudio);
        var piso = Inicio + minimo;
        return new SelecaoCorte(Inicio, Clamp(fim, piso > duracaoAudio ? duracaoAudio : piso, duracaoAudio));
    }

    /// <summary>
    /// Cria uma seleção a partir de dois pontos quaisquer (ex.: arrasto na onda), ordenando-os
    /// e garantindo a duração mínima dentro do áudio.
    /// </summary>
    public static SelecaoCorte Entre(TimeSpan a, TimeSpan b, TimeSpan duracaoAudio)
    {
        var inicio = Clamp(a < b ? a : b, TimeSpan.Zero, duracaoAudio);
        var fim = Clamp(a < b ? b : a, TimeSpan.Zero, duracaoAudio);
        var minimo = MinimoPara(duracaoAudio);
        if (fim - inicio < minimo)
        {
            fim = inicio + minimo;
            if (fim > duracaoAudio)
            {
                fim = duracaoAudio;
                inicio = fim - minimo;
            }
        }
        return new SelecaoCorte(inicio, fim);
    }

    /// <summary>True quando a seleção cobre o áudio inteiro (nada a cortar).</summary>
    public bool CobreTudo(TimeSpan duracaoAudio) =>
        Inicio <= TimeSpan.FromMilliseconds(1) && Fim >= duracaoAudio - TimeSpan.FromMilliseconds(1);

    /// <summary>Formata como <c>m:ss.fff</c> (ex.: 1:05.250).</summary>
    public static string Formatar(TimeSpan tempo)
    {
        if (tempo < TimeSpan.Zero) tempo = TimeSpan.Zero;
        return $"{(int)tempo.TotalMinutes}:{tempo.Seconds:00}.{tempo.Milliseconds:000}";
    }

    /// <summary>
    /// Interpreta <c>m:ss.fff</c>, <c>m:ss</c>, <c>ss.fff</c> ou <c>ss</c> (vírgula também aceita).
    /// </summary>
    public static bool TryParse(string? texto, out TimeSpan tempo)
    {
        tempo = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var partes = texto.Trim().Replace(',', '.').Split(':');
        if (partes.Length > 2)
            return false;

        double minutos = 0;
        if (partes.Length == 2 &&
            !double.TryParse(partes[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutos))
            return false;

        if (!double.TryParse(partes[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var segundos))
            return false;
        if (minutos < 0 || segundos < 0 || (partes.Length == 2 && segundos >= 60))
            return false;

        tempo = TimeSpan.FromMilliseconds(Math.Round((minutos * 60 + segundos) * 1000));
        return true;
    }

    private static TimeSpan MinimoPara(TimeSpan duracaoAudio) =>
        duracaoAudio < AudioTrimmer.DuracaoMinima ? duracaoAudio : AudioTrimmer.DuracaoMinima;

    private static TimeSpan Clamp(TimeSpan valor, TimeSpan min, TimeSpan max) =>
        valor < min ? min : valor > max ? max : valor;
}
