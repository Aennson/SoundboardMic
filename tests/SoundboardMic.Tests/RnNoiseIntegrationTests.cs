using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

/// <summary>
/// Fact que roda apenas se a lib nativa do rnnoise carregar neste ambiente;
/// caso contrário o teste aparece como "skipped" com o motivo.
/// </summary>
public sealed class RnNoiseFactAttribute : FactAttribute
{
    public RnNoiseFactAttribute()
    {
        if (RnNoiseProcessor.TryCreate(out var processor, out var error))
            processor!.Dispose();
        else
            Skip = $"Lib nativa do RNNoise indisponível neste ambiente: {error?.GetType().Name}";
    }
}

/// <summary>Testes de integração com a lib nativa real (win-x64).</summary>
public class RnNoiseIntegrationTests
{
    private const int SampleRate = 48000;

    private static IRnNoiseProcessor CreateProcessor()
    {
        Assert.True(RnNoiseProcessor.TryCreate(out var processor, out _));
        return processor!;
    }

    /// <summary>Processa 1 s de áudio em blocos e devolve entrada/saída (sem o warmup inicial).</summary>
    private static (float[] Input, float[] Output) ProcessOneSecond(IRnNoiseProcessor processor, Func<int, float> generator)
    {
        var input = new float[SampleRate];
        for (var i = 0; i < input.Length; i++)
            input[i] = generator(i);

        var output = (float[])input.Clone();
        const int block = 480;
        for (var pos = 0; pos < output.Length; pos += block)
            processor.Process(output, pos, block);

        // Ignora warmup (primeiro frame sai como zeros) e transitório do denoiser.
        return (input[4800..], output[4800..]);
    }

    private static double Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        foreach (var s in samples)
            sum += (double)s * s;
        return Math.Sqrt(sum / samples.Length);
    }

    [RnNoiseFact]
    public void RuidoBranco_ReduzEnergia()
    {
        using var processor = CreateProcessor();
        var random = new Random(42);

        var (input, output) = ProcessOneSecond(processor,
            _ => (float)(random.NextDouble() * 2 - 1) * 0.05f);

        var rmsIn = Rms(input);
        var rmsOut = Rms(output);

        // Ruído branco puro deve ser fortemente atenuado.
        Assert.True(rmsOut < rmsIn * 0.8,
            $"RNNoise não reduziu o ruído: RMS in={rmsIn:F5}, out={rmsOut:F5}");
    }

    [RnNoiseFact]
    public void TomDeVoz_NaoEAniquilado()
    {
        using var processor = CreateProcessor();

        var (input, output) = ProcessOneSecond(processor,
            i => 0.3f * (float)Math.Sin(2 * Math.PI * 200 * i / SampleRate));

        var rmsIn = Rms(input);  // ≈ 0.212
        var rmsOut = Rms(output);

        // Pega escala errada (±1 vs ±32768): saída sairia zerada ou estourada.
        Assert.True(rmsOut > rmsIn * 0.05,
            $"Saída aniquilada — possível escala errada: RMS in={rmsIn:F5}, out={rmsOut:F5}");
        Assert.True(rmsOut < rmsIn * 3,
            $"Saída estourada — possível escala errada: RMS in={rmsIn:F5}, out={rmsOut:F5}");
    }

    [RnNoiseFact]
    public void Process_DevolveExatamenteOsSamplesPedidos()
    {
        using var processor = CreateProcessor();

        // Blocos que NÃO são múltiplos de 480 exercitam o FIFO interno.
        var buffer = new float[1000];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = 0.1f;

        // Nenhuma exceção e o buffer inteiro é preenchido (zeros no warmup valem).
        processor.Process(buffer, 0, 700);
        processor.Process(buffer, 700, 300);

        Assert.All(buffer, sample => Assert.True(float.IsFinite(sample)));
    }
}
