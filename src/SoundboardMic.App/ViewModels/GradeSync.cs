using System.Collections.ObjectModel;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// Sincroniza as coleções da grade com o estado desejado reaproveitando os view models já
/// exibidos, em vez de limpar e recriar tudo. Como os containers da lista sobrevivem, a
/// animação de entrada não dispara de novo para os cards que não mudaram — só o card
/// realmente alterado é reanimado (ver <see cref="AudioItemViewModel.Entrando"/>).
/// </summary>
public static class GradeSync
{
    /// <summary>
    /// Alinha <paramref name="atuais"/> com <paramref name="desejados"/> por referência,
    /// movendo/inserindo no lugar e descartando o excedente ao final.
    /// </summary>
    public static void Sincronizar<T>(ObservableCollection<T> atuais, IReadOnlyList<T> desejados)
        where T : class
    {
        for (var i = 0; i < desejados.Count; i++)
        {
            var desejado = desejados[i];
            var atual = IndiceDe(atuais, desejado, i);
            if (atual < 0)
                atuais.Insert(i, desejado);
            else if (atual != i)
                atuais.Move(atual, i);
        }

        while (atuais.Count > desejados.Count)
            atuais.RemoveAt(atuais.Count - 1);
    }

    /// <summary>
    /// Alinha as seções de categoria: a seção é reaproveitada quando categoria e nome
    /// continuam os mesmos, para que só os cards afetados sejam recriados.
    /// </summary>
    public static void SincronizarGrupos(
        ObservableCollection<CategoriaGroupViewModel> atuais,
        IReadOnlyList<GrupoDesejado> desejados)
    {
        for (var i = 0; i < desejados.Count; i++)
        {
            var desejado = desejados[i];
            var atual = IndiceDoGrupo(atuais, desejado, i);

            if (atual < 0)
            {
                var novo = new CategoriaGroupViewModel(desejado.CategoriaId, desejado.Nome, desejado.Ordem);
                foreach (var item in desejado.Itens)
                    novo.Itens.Add(item);
                atuais.Insert(i, novo);
                continue;
            }

            if (atual != i)
                atuais.Move(atual, i);

            var grupo = atuais[i];
            grupo.Ordem = desejado.Ordem;
            grupo.Removendo = false;
            Sincronizar(grupo.Itens, desejado.Itens);
        }

        while (atuais.Count > desejados.Count)
            atuais.RemoveAt(atuais.Count - 1);
    }

    private static int IndiceDe<T>(ObservableCollection<T> colecao, T alvo, int inicio) where T : class
    {
        for (var i = inicio; i < colecao.Count; i++)
        {
            if (ReferenceEquals(colecao[i], alvo))
                return i;
        }
        return -1;
    }

    private static int IndiceDoGrupo(
        ObservableCollection<CategoriaGroupViewModel> colecao, GrupoDesejado alvo, int inicio)
    {
        for (var i = inicio; i < colecao.Count; i++)
        {
            if (colecao[i].CategoriaId == alvo.CategoriaId && colecao[i].Nome == alvo.Nome)
                return i;
        }
        return -1;
    }

    /// <summary>Estado desejado de uma seção da grade após um recarregamento.</summary>
    public sealed record GrupoDesejado(
        long? CategoriaId,
        string Nome,
        int Ordem,
        IReadOnlyList<AudioItemViewModel> Itens);
}
