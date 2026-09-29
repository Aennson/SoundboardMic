using System.Collections.ObjectModel;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.Models;

namespace SoundboardMic.Tests;

public class CardRemovalTests
{
    private static AudioItemViewModel Item(long id) => new(new Audio { Id = id, Nome = $"a{id}" }, null);

    [Fact]
    public async Task Remover_AnimaSoOCardEPreservaDemaisInstancias()
    {
        var a = Item(1); var b = Item(2); var c = Item(3);
        var grupo = new CategoriaGroupViewModel(1, "Memes", 0);
        foreach (var i in new[] { a, b, c }) grupo.Itens.Add(i);
        var grupos = new ObservableCollection<CategoriaGroupViewModel> { grupo };
        var audios = new ObservableCollection<AudioItemViewModel> { a, b, c };

        var tarefa = CardRemoval.RemoverAsync(grupos, audios, b, TimeSpan.FromMilliseconds(50));
        Assert.True(b.Removendo);
        Assert.False(grupo.Removendo);
        Assert.Contains(b, grupo.Itens); // continua visível durante a animação
        await tarefa;

        Assert.Same(grupo, Assert.Single(grupos)); // sem reconstrução da grade
        Assert.Equal(new[] { a, c }, grupo.Itens);
        Assert.Equal(new[] { a, c }, audios);
    }

    [Fact]
    public async Task Remover_UltimoCardAnimaESomeComASecao()
    {
        var a = Item(1); var b = Item(2);
        var memes = new CategoriaGroupViewModel(1, "Memes", 0);
        memes.Itens.Add(a);
        var outros = new CategoriaGroupViewModel(null, "Sem categoria", int.MaxValue);
        outros.Itens.Add(b);
        var grupos = new ObservableCollection<CategoriaGroupViewModel> { memes, outros };
        var audios = new ObservableCollection<AudioItemViewModel> { a, b };

        var tarefa = CardRemoval.RemoverAsync(grupos, audios, a, TimeSpan.FromMilliseconds(50));
        Assert.True(memes.Removendo);
        await tarefa;

        Assert.Same(outros, Assert.Single(grupos));
        Assert.Equal(new[] { b }, audios);
    }
}
