namespace SoundboardMic.Core.Models;

/// <summary>
/// Associação entre um áudio e uma combinação de teclas global.
/// </summary>
public class Mapeamento
{
    public long Id { get; set; }

    /// <summary>FK para <see cref="Audio.Id"/>.</summary>
    public long AudioId { get; set; }

    /// <summary>Combinação serializada, ex.: "Ctrl+Alt+F1".</summary>
    public string Teclas { get; set; } = string.Empty;

    /// <summary>Se falso, o atalho não dispara o áudio.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Data/hora de criação em UTC.</summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
