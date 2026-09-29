using NAudio.Wave;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>Repete o áudio decodificado antes do resampling, sem inserir silêncio entre ciclos.</summary>
public sealed class LoopingWaveStream(WaveStream source) : WaveStream
{
    public override WaveFormat WaveFormat => source.WaveFormat;
    public override long Length => source.Length;
    public override long Position
    {
        get => source.Position;
        set => source.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count)
            throw new ArgumentException("O intervalo solicitado excede o buffer.");

        var total = 0;
        while (total < count)
        {
            var read = source.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                source.Position = 0;
                read = source.Read(buffer, offset + total, count - total);
                if (read == 0) break; // Arquivos vazios não podem prender a thread de áudio.
            }
            total += read;
        }
        return total;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) source.Dispose();
        base.Dispose(disposing);
    }
}
