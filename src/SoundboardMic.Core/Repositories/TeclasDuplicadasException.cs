namespace SoundboardMic.Core.Repositories;

/// <summary>
/// Lançada ao tentar gravar um mapeamento cuja combinação de teclas
/// já está associada a outro áudio.
/// </summary>
public class TeclasDuplicadasException : Exception
{
    public string Teclas { get; }

    public TeclasDuplicadasException(string teclas, Exception? inner = null)
        : base($"A combinação de teclas \"{teclas}\" já está em uso por outro mapeamento.", inner)
    {
        Teclas = teclas;
    }
}
