using NAudio.Wave;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Corta um áudio mantendo um único trecho contínuo e o grava como WAV PCM 16-bit, na
/// mesma taxa de amostragem e número de canais da decodificação. Tudo é feito em fluxo
/// (sem carregar o arquivo inteiro na memória), com precisão de frame e um micro fade
/// nas bordas para não gerar estalos quando o corte cai fora de um cruzamento por zero.
/// </summary>
public static class AudioTrimmer
{
    /// <summary>Menor trecho aceito pelo editor.</summary>
    public static readonly TimeSpan DuracaoMinima = TimeSpan.FromMilliseconds(100);

    /// <summary>Fade aplicado em cada borda do trecho.</summary>
    public static readonly TimeSpan FadePadrao = TimeSpan.FromMilliseconds(5);

    /// <summary>Corta o arquivo em <paramref name="caminho"/> e devolve os bytes do WAV resultante.</summary>
    /// <exception cref="FileNotFoundException">Arquivo inexistente.</exception>
    /// <exception cref="FormatoNaoSuportadoException">Formato inválido ou corrompido.</exception>
    public static byte[] CortarParaWav(string caminho, TimeSpan inicio, TimeSpan fim)
    {
        using var reader = AudioFileDecoder.OpenRead(caminho);
        using var saida = new MemoryStream();
        Cortar(AudioFileDecoder.ToSampleProvider(reader), inicio, fim, saida, FadePadrao);
        return saida.ToArray();
    }

    /// <summary>
    /// Copia de <paramref name="origem"/> apenas os frames em [inicio, fim) para
    /// <paramref name="destino"/> como WAV PCM 16-bit. Retorna a quantidade de frames gravados.
    /// </summary>
    public static long Cortar(ISampleProvider origem, TimeSpan inicio, TimeSpan fim, Stream destino, TimeSpan fade)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(destino);
        if (inicio < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(inicio), "O início não pode ser negativo.");
        if (fim - inicio < DuracaoMinima)
            throw new ArgumentOutOfRangeException(nameof(fim),
                $"O trecho precisa ter pelo menos {DuracaoMinima.TotalMilliseconds:0} ms.");

        var formato = origem.WaveFormat;
        var canais = formato.Channels;
        var taxa = formato.SampleRate;
        var frameInicio = ParaFrames(inicio, taxa);
        var frameFim = ParaFrames(fim, taxa);
        var framesFade = Math.Max(1, ParaFrames(fade, taxa));

        // O writer escreve o cabeçalho no Dispose; o stream de destino continua aberto.
        using var writer = new WaveFileWriter(new NaoFecharStream(destino), new WaveFormat(taxa, 16, canais));

        var buffer = new float[Math.Max(1, taxa / 10) * canais]; // ~100 ms, múltiplo de canais
        long frameAtual = 0;
        long gravados = 0;
        int lidos;
        while (frameAtual < frameFim && (lidos = origem.Read(buffer, 0, buffer.Length)) > 0)
        {
            var framesLidos = lidos / canais;
            for (var f = 0; f < framesLidos; f++, frameAtual++)
            {
                if (frameAtual < frameInicio) continue;
                if (frameAtual >= frameFim) break;

                var ganho = GanhoFade(frameAtual - frameInicio, frameFim - frameAtual - 1, framesFade);
                for (var c = 0; c < canais; c++)
                    writer.WriteSample(Math.Clamp(buffer[f * canais + c] * ganho, -1f, 1f));
                gravados++;
            }
        }

        if (gravados == 0)
            throw new ArgumentOutOfRangeException(nameof(inicio), "O trecho selecionado está além do fim do áudio.");

        return gravados;
    }

    /// <summary>Converte um tempo em índice de frame (arredondado para o frame mais próximo).</summary>
    public static long ParaFrames(TimeSpan tempo, int taxaAmostragem) =>
        (long)Math.Round(tempo.TotalSeconds * taxaAmostragem, MidpointRounding.AwayFromZero);

    private static float GanhoFade(long desdeInicio, long ateFim, long framesFade)
    {
        var ganho = 1f;
        if (desdeInicio < framesFade)
            ganho = Math.Min(ganho, (float)desdeInicio / framesFade);
        if (ateFim < framesFade)
            ganho = Math.Min(ganho, (float)ateFim / framesFade);
        return ganho;
    }

    /// <summary>Impede o <see cref="WaveFileWriter"/> de fechar o stream do chamador.</summary>
    private sealed class NaoFecharStream(Stream interno) : Stream
    {
        public override bool CanRead => interno.CanRead;
        public override bool CanSeek => interno.CanSeek;
        public override bool CanWrite => interno.CanWrite;
        public override long Length => interno.Length;
        public override long Position { get => interno.Position; set => interno.Position = value; }
        public override void Flush() => interno.Flush();
        public override int Read(byte[] buffer, int offset, int count) => interno.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => interno.Seek(offset, origin);
        public override void SetLength(long value) => interno.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => interno.Write(buffer, offset, count);
        protected override void Dispose(bool disposing) => Flush();
    }
}
