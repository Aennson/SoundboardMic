using System.Collections.ObjectModel;

namespace SoundboardMic.App.ViewModels;

/// <summary>Reordenação de cards dentro da mesma categoria (arrastar e soltar).</summary>
public static class AudioOrdering
{
    /// <summary>
    /// Move <paramref name="origem"/> para a posição de <paramref name="destino"/> e renumera
    /// <see cref="Core.Models.Audio.Ordem"/> sequencialmente. Retorna os itens alterados, ou
    /// vazio se os cards não pertencem à mesma lista ou são o mesmo.
    /// </summary>
    public static IReadOnlyList<AudioItemViewModel> Mover(
        ObservableCollection<AudioItemViewModel> itens, AudioItemViewModel origem, AudioItemViewModel destino)
    {
        var de = itens.IndexOf(origem);
        var para = itens.IndexOf(destino);
        if (de < 0 || para < 0 || de == para)
            return Array.Empty<AudioItemViewModel>();

        itens.Move(de, para);
        var alterados = new List<AudioItemViewModel>();
        for (var i = 0; i < itens.Count; i++)
        {
            if (itens[i].Audio.Ordem == i) continue;
            itens[i].Audio.Ordem = i;
            alterados.Add(itens[i]);
        }
        return alterados;
    }
}
