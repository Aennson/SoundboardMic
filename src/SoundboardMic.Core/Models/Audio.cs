namespace SoundboardMic.Core.Models;

/// <summary>
/// Um arquivo de áudio cadastrado no soundboard.
/// </summary>
public class Audio
{
    public long Id { get; set; }

    /// <summary>Nome amigável exibido na interface.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Caminho absoluto do arquivo de áudio no disco. Aponta para uma cópia gerenciada
    /// pelo próprio app (cache local materializado a partir de <see cref="ArquivoConteudo"/>),
    /// não para o arquivo original escolhido pelo usuário — assim tocar o som não depende
    /// do arquivo original continuar existindo/no mesmo lugar.
    /// </summary>
    public string CaminhoArquivo { get; set; } = string.Empty;

    /// <summary>
    /// Conteúdo binário do arquivo de áudio, persistido no banco (BLOB). É a fonte de
    /// verdade: se o cache em disco (<see cref="CaminhoArquivo"/>) for perdido, ele é
    /// regenerado a partir daqui. Null apenas para registros legados ainda não migrados.
    /// </summary>
    public byte[]? ArquivoConteudo { get; set; }

    /// <summary>Nome do arquivo original escolhido pelo usuário (exibição/extensão).</summary>
    public string? ArquivoNomeOriginal { get; set; }

    /// <summary>Duração do áudio em milissegundos (0 se desconhecida).</summary>
    public long DuracaoMs { get; set; }

    /// <summary>Volume individual do áudio (0.0 a 2.0; 1.0 = original).</summary>
    public double VolumePadrao { get; set; } = 1.0;

    /// <summary>Code-point hex do glifo na barra rápida (Segoe Fluent), ex. "E8D6". Null = padrão.</summary>
    public string? Icone { get; set; }

    /// <summary>Cor de fundo do botão na barra rápida ("#RRGGBB"). Null = padrão.</summary>
    public string? Cor { get; set; }

    /// <summary>Data/hora de criação em UTC.</summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
