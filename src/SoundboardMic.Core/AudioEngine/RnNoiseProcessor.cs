using RNNoise.NET;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Implementação real de <see cref="IRnNoiseProcessor"/> sobre a lib nativa
/// rnnoise (pacote YellowDogMan.RRNoise.NET). O wrapper já converte a escala
/// de samples (±1 ↔ ±32768) e bufferiza frames de 480 samples internamente,
/// mas pode devolver menos samples do que recebe; um FIFO local garante o
/// contrato "entra N, sai N" preenchendo com zeros apenas no warmup
/// (latência fixa de até 480 samples = 10 ms em 48 kHz).
/// </summary>
public sealed class RnNoiseProcessor : IRnNoiseProcessor
{
    private Denoiser _denoiser;
    private float[] _scratch = new float[4800];
    private float[] _fifo = new float[9600];
    private int _fifoCount;

    private RnNoiseProcessor(Denoiser denoiser)
    {
        _denoiser = denoiser;
    }

    /// <summary>
    /// Cria o processador forçando o load da lib nativa imediatamente.
    /// Retorna false (com o erro) se a DLL não carregar neste sistema —
    /// o motor então segue sem RNNoise, apenas com o noise gate.
    /// </summary>
    public static bool TryCreate(out IRnNoiseProcessor? processor, out Exception? error)
    {
        processor = null;
        error = null;
        try
        {
            // Sonda descartável: o Denoise força o P/Invoke já aqui.
            using (var probe = new Denoiser())
            {
                Span<float> frame = stackalloc float[480];
                probe.Denoise(frame, false);
            }

            processor = new RnNoiseProcessor(new Denoiser());
            return true;
        }
        catch (Exception ex)
        {
            // DllNotFound/BadImageFormat/TypeInitialization etc. — qualquer
            // falha de load vira indisponibilidade, nunca crash do app.
            error = ex;
            return false;
        }
    }

    public void Process(float[] buffer, int offset, int count)
    {
        if (count <= 0)
            return;

        if (_scratch.Length < count)
            _scratch = new float[count * 2];

        Array.Copy(buffer, offset, _scratch, 0, count);
        var written = _denoiser.Denoise(_scratch.AsSpan(0, count), false);

        // Acumula a saída denoised e devolve exatamente `count` samples,
        // completando com zeros na frente enquanto o denoiser ainda enche
        // o primeiro frame (warmup).
        AppendToFifo(_scratch, written);

        var available = Math.Min(count, _fifoCount);
        var shortfall = count - available;

        Array.Clear(buffer, offset, shortfall);
        Array.Copy(_fifo, 0, buffer, offset + shortfall, available);

        _fifoCount -= available;
        Array.Copy(_fifo, available, _fifo, 0, _fifoCount);
    }

    public void Reset()
    {
        _denoiser.Dispose();
        _denoiser = new Denoiser();
        _fifoCount = 0;
    }

    public void Dispose()
    {
        _denoiser.Dispose();
    }

    private void AppendToFifo(float[] data, int count)
    {
        if (_fifoCount + count > _fifo.Length)
        {
            var grown = new float[(_fifoCount + count) * 2];
            Array.Copy(_fifo, grown, _fifoCount);
            _fifo = grown;
        }

        Array.Copy(data, 0, _fifo, _fifoCount, count);
        _fifoCount += count;
    }
}
