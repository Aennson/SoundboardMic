namespace SoundboardMic.Core.Models;

/// <summary>
/// Um arquivo de áudio cadastrado no soundboard.
/// </summary>
public class Audio
{
    public long Id { get; set; }

    /// <summary>Nome amigável exibido na interface.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Caminho absoluto do arquivo de áudio no disco.</summary>
    public string CaminhoArquivo { get; set; } = string.Empty;

    /// <summary>Duração do áudio em milissegundos (0 se desconhecida).</summary>
    public long DuracaoMs { get; set; }

    /// <summary>Volume individual do áudio (0.0 a 2.0; 1.0 = original).</summary>
    public double VolumePadrao { get; set; } = 1.0;

    /// <summary>Data/hora de criação em UTC.</summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
