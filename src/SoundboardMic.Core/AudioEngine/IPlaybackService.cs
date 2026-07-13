namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Reprodução simples de um arquivo por vez em um dispositivo de saída escolhido.
/// Usado para preview/teste local. (O mixing com o microfone é feito pelo motor
/// de injeção, na etapa 3.)
/// </summary>
public interface IPlaybackService : IDisposable
{
    bool IsPlaying { get; }

    /// <summary>Disparado quando a reprodução termina (fim do arquivo ou Stop).</summary>
    event EventHandler? PlaybackStopped;

    /// <summary>
    /// Reproduz o arquivo no dispositivo indicado. Interrompe qualquer reprodução anterior.
    /// </summary>
    /// <param name="filePath">Caminho do arquivo (.mp3, .wav, .ogg).</param>
    /// <param name="deviceId">ID do dispositivo de saída; null = padrão do sistema.</param>
    /// <param name="volume">Volume linear (0.0 a 2.0; 1.0 = original).</param>
    void Play(string filePath, string? deviceId = null, float volume = 1.0f);

    /// <summary>Para a reprodução atual imediatamente (no-op se nada tocando).</summary>
    void Stop();
}
