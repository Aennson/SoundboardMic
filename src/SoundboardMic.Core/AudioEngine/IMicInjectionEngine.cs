namespace SoundboardMic.Core.AudioEngine;

/// <summary>Dados do encerramento inesperado do motor (ex.: dispositivo desconectado).</summary>
public class EngineStoppedEventArgs : EventArgs
{
    public Exception? Exception { get; init; }
}

/// <summary>
/// Motor de injeção: captura o microfone físico, mixa com os áudios do soundboard
/// e envia o resultado para o dispositivo virtual (CABLE Input). Opcionalmente
/// reproduz os sons do soundboard também nos fones (monitoramento local).
/// </summary>
public interface IMicInjectionEngine : IDisposable
{
    bool IsRunning { get; }

    /// <summary>Quantidade de sons do soundboard tocando agora.</summary>
    int ActiveSoundCount { get; }

    /// <summary>Snapshot dos caminhos dos sons tocando agora no mix principal (sem duplicar o monitor).</summary>
    IReadOnlyList<string> GetActiveSoundPaths();

    /// <summary>Volume do microfone no mix (0.0 a 2.0). Ajustável em tempo real.</summary>
    float MicVolume { get; set; }

    /// <summary>Volume master do soundboard (0.0 a 2.0). Ajustável em tempo real.</summary>
    float SoundboardVolume { get; set; }

    /// <summary>Liga/desliga o monitoramento local. Ajustável em tempo real.</summary>
    bool MonitorEnabled { get; set; }

    /// <summary>Liga/desliga a supressão de ruído (RNNoise) do mic. Ajustável em tempo real.</summary>
    bool NoiseSuppressionEnabled { get; set; }

    /// <summary>Liga/desliga o noise gate do mic. Ajustável em tempo real.</summary>
    bool NoiseGateEnabled { get; set; }

    /// <summary>
    /// False se a lib nativa do RNNoise não carregou no último Start —
    /// o toggle de supressão deve ficar desabilitado na UI.
    /// </summary>
    bool NoiseSuppressionAvailable { get; }

    /// <summary>Disparado quando ActiveSoundCount muda (som iniciou ou terminou).</summary>
    event EventHandler? ActiveSoundsChanged;

    /// <summary>Disparado quando o motor para sem Stop() (dispositivo desconectado etc.).</summary>
    event EventHandler<EngineStoppedEventArgs>? StoppedUnexpectedly;

    /// <summary>Inicia captura do mic + saída mixada. Lança se já estiver rodando.</summary>
    void Start(MicInjectionOptions options);

    /// <summary>Para tudo e libera os dispositivos (no-op se parado).</summary>
    void Stop();

    /// <summary>
    /// Dispara um som do soundboard no mix (e no monitor, se habilitado).
    /// Vários sons podem tocar simultaneamente.
    /// </summary>
    /// <exception cref="InvalidOperationException">Motor não está rodando.</exception>
    void PlaySound(string filePath, float volume = 1.0f);

    /// <summary>Tecla de pânico: interrompe todos os sons do soundboard (mic continua).</summary>
    void StopAllSounds();
}
