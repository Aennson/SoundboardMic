using System.Collections.ObjectModel;
using System.ComponentModel;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.Models;

namespace SoundboardMic.Tests;

public class GradeSyncTests
{
    private static AudioItemViewModel Item(long id, long? categoria = null) =>
        new(new Audio { Id = id, Nome = $"a{id}", CategoriaId = categoria }, null);

    private static GradeSync.GrupoDesejado Grupo(long? id, string nome, params AudioItemViewModel[] itens) =>
        new(id, nome, 0, itens);

    [Fact]
    public void SincronizarGrupos_ReaproveitaSecoesECardsExistentes()
    {
        var a = Item(1, 1); var b = Item(2, 1); var c = Item(3, 1);
        var grupos = new ObservableCollection<CategoriaGroupViewModel>();

        GradeSync.SincronizarGrupos(grupos, new[] { Grupo(1, "Memes", a, b, c) });
        var secao = Assert.Single(grupos);

        // Recarregar com o mesmo conteúdo não pode recriar seção nem cards: é isso que
        // impede a grade inteira de reanimar quando só um áudio foi salvo.
        GradeSync.SincronizarGrupos(grupos, new[] { Grupo(1, "Memes", a, b, c) });

        Assert.Same(secao, Assert.Single(grupos));
        Assert.Equal(new[] { a, b, c }, secao.Itens);
    }

    [Fact]
    public void SincronizarGrupos_AplicaInsercoesRemocoesEMovimentacoesEntreSecoes()
    {
        var a = Item(1, 1); var b = Item(2, 1); var c = Item(3, 2); var d = Item(4, 1);
        var grupos = new ObservableCollection<CategoriaGroupViewModel>();
        GradeSync.SincronizarGrupos(grupos, new[] { Grupo(1, "Memes", a, b), Grupo(2, "Jogos", c) });
        var memes = grupos[0];
        var jogos = grupos[1];

        // b muda de categoria, d entra em Memes e a ordem das seções inverte.
        GradeSync.SincronizarGrupos(grupos, new[] { Grupo(2, "Jogos", c, b), Grupo(1, "Memes", a, d) });

        Assert.Same(jogos, grupos[0]);
        Assert.Same(memes, grupos[1]);
        Assert.Equal(new[] { c, b }, jogos.Itens);
        Assert.Equal(new[] { a, d }, memes.Itens);

        // Seção que fica vazia sai da grade.
        GradeSync.SincronizarGrupos(grupos, new[] { Grupo(2, "Jogos", c, b) });
        Assert.Same(jogos, Assert.Single(grupos));
    }

    [Fact]
    public void SincronizarGrupos_RecriaSecaoQuandoCategoriaERenomeada()
    {
        var a = Item(1, 1);
        var grupos = new ObservableCollection<CategoriaGroupViewModel>();
        GradeSync.SincronizarGrupos(grupos, new[] { Grupo(1, "Memes", a) });
        var original = grupos[0];

        GradeSync.SincronizarGrupos(grupos, new[] { Grupo(1, "Clássicos", a) });

        var atual = Assert.Single(grupos);
        Assert.NotSame(original, atual);
        Assert.Equal("Clássicos", atual.Nome);
    }

    [Fact]
    public void Atualizar_TrocaModeloENotificaPropriedadesDerivadas()
    {
        var item = Item(7);
        var notificadas = new List<string?>();
        ((INotifyPropertyChanged)item).PropertyChanged += (_, e) => notificadas.Add(e.PropertyName);

        var novo = new Audio
        {
            Id = 7,
            Nome = "novo nome",
            CaminhoArquivo = @"C:\sons\novo.wav",
            DuracaoMs = 65_000,
            VolumePadrao = 0.5f,
            Icone = "E8B1",
            Cor = "#FF0000",
        };
        item.Atualizar(novo, new Mapeamento { AudioId = 7, Teclas = "Ctrl+1", Ativo = false });

        Assert.Same(novo, item.Audio);
        Assert.Equal("novo nome", item.Nome);
        Assert.Equal("1:05", item.DuracaoTexto);
        Assert.Equal("50%", item.VolumeTexto);
        Assert.True(item.TemAtalho);
        Assert.False(item.Ativo);
        Assert.Contains(nameof(AudioItemViewModel.Nome), notificadas);
        Assert.Contains(nameof(AudioItemViewModel.DuracaoTexto), notificadas);
        Assert.Contains(nameof(AudioItemViewModel.VolumeTexto), notificadas);
        Assert.Contains(nameof(AudioItemViewModel.Teclas), notificadas);
    }
}
