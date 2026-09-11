using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// Uma seção/categoria de exibição na grade de "Meus sons": um cabeçalho (nome +
/// ordem) e os cards de áudio que pertencem a ela. "Sem categoria" é representada
/// com <see cref="CategoriaId"/> nulo e sempre aparece por último.
/// </summary>
public partial class CategoriaGroupViewModel : ObservableObject
{
    public CategoriaGroupViewModel(long? categoriaId, string nome, int ordem)
    {
        CategoriaId = categoriaId;
        Nome = nome;
        Ordem = ordem;
    }

    public long? CategoriaId { get; }
    public string Nome { get; }

    /// <summary>Posição de exibição relativa às demais categorias (crescente).</summary>
    [ObservableProperty] private int _ordem;

    /// <summary>True para a seção "sem categoria" — não é reordenável.</summary>
    public bool SemCategoria => CategoriaId is null;

    public ObservableCollection<AudioItemViewModel> Itens { get; } = new();
}
