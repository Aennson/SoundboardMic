using System.Collections.ObjectModel;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.Models;

namespace SoundboardMic.Tests;

public class AudioOrderingTests
{
    private static ObservableCollection<AudioItemViewModel> Itens(params string[] nomes) =>
        new(nomes.Select((n, i) => new AudioItemViewModel(new Audio { Id = i + 1, Nome = n, Ordem = 0 }, null)));

    [Fact]
    public void Mover_ParaFrente_InsereNaPosicaoDoDestinoERenumera()
    {
        var itens = Itens("a", "b", "c", "d");

        var alterados = AudioOrdering.Mover(itens, itens[0], itens[2]);

        Assert.Equal(new[] { "b", "c", "a", "d" }, itens.Select(i => i.Nome));
        Assert.Equal(new[] { 0, 1, 2, 3 }, itens.Select(i => i.Audio.Ordem));
        Assert.Equal(3, alterados.Count); // "b" já estava com Ordem 0
    }

    [Fact]
    public void Mover_ParaTras_InsereNaPosicaoDoDestino()
    {
        var itens = Itens("a", "b", "c", "d");

        AudioOrdering.Mover(itens, itens[3], itens[1]);

        Assert.Equal(new[] { "a", "d", "b", "c" }, itens.Select(i => i.Nome));
        Assert.Equal(new[] { 0, 1, 2, 3 }, itens.Select(i => i.Audio.Ordem));
    }

    [Fact]
    public void Mover_MesmoItemOuForaDaLista_NaoAltera()
    {
        var itens = Itens("a", "b");
        var externo = Itens("x")[0];

        Assert.Empty(AudioOrdering.Mover(itens, itens[0], itens[0]));
        Assert.Empty(AudioOrdering.Mover(itens, externo, itens[1]));
        Assert.Equal(new[] { "a", "b" }, itens.Select(i => i.Nome));
    }
}
