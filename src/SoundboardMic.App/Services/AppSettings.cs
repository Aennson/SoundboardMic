namespace SoundboardMic.App.Services;

/// <summary>
/// Configurações persistidas do usuário (JSON em %LOCALAPPDATA%\SoundboardMic\settings.json).
/// </summary>
public class AppSettings
{
    /// <summary>ID do microfone físico de entrada. Null = padrão do sistema.</summary>
    public string? MicDeviceId { get; set; }

    /// <summary>ID do dispositivo de saída virtual (CABLE Input). Null = padrão.</summary>
    public string? OutputDeviceId { get; set; }

    /// <summary>ID do dispositivo de monitoramento local (fones). Null = padrão.</summary>
    public string? MonitorDeviceId { get; set; }

    /// <summary>Volume do microfone no mix (0.0 a 2.0).</summary>
    public float MicVolume { get; set; } = 1.0f;

    /// <summary>Volume master do soundboard (0.0 a 2.0).</summary>
    public float SoundboardVolume { get; set; } = 1.0f;

    /// <summary>Se o monitoramento local está habilitado.</summary>
    public bool MonitorEnabled { get; set; }

    /// <summary>Supressão de ruído do microfone via RNNoise.</summary>
    public bool NoiseSuppressionEnabled { get; set; }

    /// <summary>Noise gate do microfone (silencia abaixo do limiar).</summary>
    public bool NoiseGateEnabled { get; set; }

    /// <summary>Combinação da tecla de pânico (para todos os sons). Null = desabilitada.</summary>
    public string? PanicKey { get; set; } = "Ctrl+Alt+P";

    /// <summary>Iniciar o motor de injeção automaticamente ao abrir o app.</summary>
    public bool AutoStartEngine { get; set; } = true;

    /// <summary>Iniciar o app junto com o Windows.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Minimizar para a bandeja em vez de fechar.</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>Se a barra rápida flutuante está visível.</summary>
    public bool QuickBarVisible { get; set; }

    /// <summary>Orientação da barra rápida: "Horizontal" ou "Vertical".</summary>
    public string QuickBarOrientation { get; set; } = "Horizontal";

    /// <summary>Posição salva da barra rápida (null = default no canto inferior esquerdo).</summary>
    public double? QuickBarLeft { get; set; }
    public double? QuickBarTop { get; set; }
}
