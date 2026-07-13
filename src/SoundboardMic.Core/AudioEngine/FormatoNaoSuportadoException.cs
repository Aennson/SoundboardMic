namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Lançada ao tentar abrir um arquivo cujo formato não é suportado
/// (extensão desconhecida ou conteúdo inválido/corrompido).
/// </summary>
public class FormatoNaoSuportadoException : Exception
{
    public string CaminhoArquivo { get; }

    public FormatoNaoSuportadoException(string caminhoArquivo, Exception? inner = null)
        : base($"Formato de áudio não suportado ou arquivo inválido: {caminhoArquivo}", inner)
    {
        CaminhoArquivo = caminhoArquivo;
    }
}
