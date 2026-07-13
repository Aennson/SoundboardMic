namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Configuração de inicialização do motor de injeção.
/// </summary>
public class MicInjectionOptions
{
    /// <summary>Microfone físico (capture). Null = padrão do sistema.</summary>
    public string? MicDeviceId { get; set; }

    /// <summary>
    /// Dispositivo de saída virtual (normalmente o "CABLE Input" do VB-Audio).
    /// Null = saída padrão do sistema (útil apenas para testes).
    /// </summary>
    public string? OutputDeviceId { get; set; }

    /// <summary>Dispositivo para monitoramento local (fones). Null = padrão.</summary>
    public string? MonitorDeviceId { get; set; }

    /// <summary>Se o monitoramento local começa habilitado.</summary>
    public bool MonitorEnabled { get; set; }

    /// <summary>Se a supressão de ruído RNNoise do microfone começa habilitada.</summary>
    public bool NoiseSuppressionEnabled { get; set; }

    /// <summary>Se o noise gate do microfone começa habilitado.</summary>
    public bool NoiseGateEnabled { get; set; }

    /// <summary>Volume do microfone no mix (0.0 a 2.0).</summary>
    public float MicVolume { get; set; } = 1.0f;

    /// <summary>Volume master do soundboard no mix (0.0 a 2.0).</summary>
    public float SoundboardVolume { get; set; } = 1.0f;
}
