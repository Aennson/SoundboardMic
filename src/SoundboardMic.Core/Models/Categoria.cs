namespace SoundboardMic.Core.Models;

/// <summary>
/// Categoria/grupo de organização usado para agrupar áudios no acervo
/// ("Meus sons"). Cada categoria ocupa uma seção própria na exibição.
/// </summary>
public class Categoria
{
    public long Id { get; set; }

    /// <summary>Nome exibido no cabeçalho da seção.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Posição de exibição relativa às demais categorias (crescente).</summary>
    public int Ordem { get; set; }
}
