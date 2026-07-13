namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Abstrai o denoiser RNNoise (lib nativa) para que a cadeia de áudio e os
/// testes não dependam da DLL: fakes implementam esta interface sem P/Invoke.
/// A escala de samples esperada pela lib fica encapsulada na implementação real.
/// </summary>
public interface IRnNoiseProcessor : IDisposable
{
    /// <summary>Processa in-place samples float mono 48 kHz (escala ±1).</summary>
    void Process(float[] buffer, int offset, int count);

    /// <summary>Limpa o estado interno (usado ao religar o toggle).</summary>
    void Reset();
}
