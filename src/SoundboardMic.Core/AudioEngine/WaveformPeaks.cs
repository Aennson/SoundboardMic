using NAudio.Wave;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Resumo da forma de onda para exibição: o pico absoluto (0..1) de cada coluna, com os
/// canais combinados numa única onda. Calculado em fluxo, sem manter o áudio na memória.
/// </summary>
public sealed record WaveformPeaks(float[] Picos, TimeSpan Duracao, int TaxaAmostragem, int Canais)
{
    /// <summary>Resolução padrão: suficiente para telas largas; a view reamostra para a largura real.</summary>
    public const int ColunasPadrao = 2048;

    /// <summary>Lê o arquivo e calcula os picos.</summary>
    public static WaveformPeaks Calcular(string caminho, int colunas = ColunasPadrao, CancellationToken ct = default)
    {
        using var reader = AudioFileDecoder.OpenRead(caminho);
        var provider = AudioFileDecoder.ToSampleProvider(reader);
        var bytesPorFrame = Math.Max(1, reader.WaveFormat.BlockAlign);
        var totalFrames = reader.Length / bytesPorFrame;
        return Calcular(provider, totalFrames, colunas, ct);
    }

    /// <summary>
    /// Calcula os picos de <paramref name="origem"/>, distribuindo <paramref name="totalFrames"/>
    /// (estimativa) em <paramref name="colunas"/>. Frames excedentes caem na última coluna.
    /// </summary>
    public static WaveformPeaks Calcular(ISampleProvider origem, long totalFrames, int colunas, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(origem);
        if (colunas <= 0) throw new ArgumentOutOfRangeException(nameof(colunas));

        var canais = origem.WaveFormat.Channels;
        var taxa = origem.WaveFormat.SampleRate;
        var framesPorColuna = Math.Max(1.0, (double)Math.Max(totalFrames, 1) / colunas);
        var picos = new float[colunas];

        var buffer = new float[Math.Max(1, taxa / 10) * canais];
        long frame = 0;
        int lidos;
        while ((lidos = origem.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            var framesLidos = lidos / canais;
            for (var f = 0; f < framesLidos; f++, frame++)
            {
                var coluna = (int)Math.Min(colunas - 1, frame / framesPorColuna);
                var pico = picos[coluna];
                for (var c = 0; c < canais; c++)
                {
                    var v = Math.Abs(buffer[f * canais + c]);
                    if (v > pico) pico = v;
                }
                picos[coluna] = Math.Min(1f, pico);
            }
        }

        // Com poucas amostras sobram colunas vazias no fim; recorta para o que foi lido.
        var colunasUsadas = (int)Math.Clamp(Math.Ceiling(frame / framesPorColuna), 1, colunas);
        if (colunasUsadas < colunas)
            Array.Resize(ref picos, colunasUsadas);

        return new WaveformPeaks(picos, TimeSpan.FromSeconds((double)frame / taxa), taxa, canais);
    }

    /// <summary>Reamostra os picos para <paramref name="largura"/> colunas (máximo por faixa).</summary>
    public float[] Reamostrar(int largura)
    {
        if (largura <= 0 || Picos.Length == 0)
            return Array.Empty<float>();

        var resultado = new float[largura];
        var escala = (double)Picos.Length / largura;
        for (var x = 0; x < largura; x++)
        {
            var de = (int)(x * escala);
            var ate = Math.Max(de + 1, (int)Math.Ceiling((x + 1) * escala));
            ate = Math.Min(ate, Picos.Length);
            var pico = 0f;
            for (var i = Math.Min(de, Picos.Length - 1); i < ate; i++)
                if (Picos[i] > pico) pico = Picos[i];
            resultado[x] = pico;
        }
        return resultado;
    }
}
