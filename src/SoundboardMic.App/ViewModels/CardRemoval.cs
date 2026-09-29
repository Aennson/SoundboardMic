using System.Collections.ObjectModel;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// Remove um card da grade sem reconstruí-la: marca a saída animada (do card, ou da seção
/// inteira se era o último da categoria), aguarda a animação e só então tira o item das
/// coleções. Os demais cards permanecem e apenas deslizam para ocupar o espaço, sem piscar.
/// </summary>
public static class CardRemoval
{
    public static async Task RemoverAsync(
        ObservableCollection<CategoriaGroupViewModel> grupos,
        ObservableCollection<AudioItemViewModel> audios,
        AudioItemViewModel item,
        TimeSpan duracaoAnimacao)
    {
        var grupo = grupos.FirstOrDefault(g => g.Itens.Contains(item));
        if (grupo is { Itens.Count: 1 })
            grupo.Removendo = true;
        else
            item.Removendo = true;

        await Task.Delay(duracaoAnimacao);

        if (grupo is not null)
        {
            grupo.Itens.Remove(item);
            if (grupo.Itens.Count == 0)
                grupos.Remove(grupo);
        }
        audios.Remove(item);
    }
}
