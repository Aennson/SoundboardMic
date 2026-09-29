using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SoundboardMic.App.Behaviors;

/// <summary>
/// Entrada da grade espelhando a animação de exclusão: cada seção de categoria cresce na vertical
/// e esmaece para dentro, uma após a outra; os cards da seção surgem crescendo logo em seguida.
/// Aplicado aos containers via <see cref="GrupoProperty"/> (seção) e <see cref="CardProperty"/> (card).
/// Fora de seções (ex.: "Sons da Web"), os cards se escalonam pela própria posição na lista.
/// Roda ao carregar e sempre que a tela volta a ficar visível (troca de aba na barra lateral).
/// </summary>
public static class EntradaEscalonada
{
    public static readonly TimeSpan IntervaloGrupos = TimeSpan.FromMilliseconds(110);
    public static readonly TimeSpan IntervaloCards = TimeSpan.FromMilliseconds(30);
    public static readonly TimeSpan DuracaoGrupo = TimeSpan.FromMilliseconds(280);
    public static readonly TimeSpan DuracaoCard = TimeSpan.FromMilliseconds(260);
    private static readonly TimeSpan JanelaRodada = TimeSpan.FromMilliseconds(250);
    private const int MaxGruposEscalonados = 8;
    private const int MaxCardsEscalonados = 10;

    // Seções que entram juntas (abrir a tela) formam uma "rodada" escalonada; seções que entram
    // isoladas depois (ex.: rolagem virtualizada) aparecem sem espera.
    private static long _ultimoRegistro;
    private static int _contagemRodada;

    public static readonly DependencyProperty GrupoProperty = DependencyProperty.RegisterAttached(
        "Grupo", typeof(bool), typeof(EntradaEscalonada), new PropertyMetadata(false, OnGrupoChanged));

    public static readonly DependencyProperty CardProperty = DependencyProperty.RegisterAttached(
        "Card", typeof(bool), typeof(EntradaEscalonada), new PropertyMetadata(false, OnCardChanged));

    private static readonly DependencyProperty InicioProperty = DependencyProperty.RegisterAttached(
        "Inicio", typeof(long), typeof(EntradaEscalonada), new PropertyMetadata(0L));

    private static readonly DependencyProperty AtrasoProperty = DependencyProperty.RegisterAttached(
        "Atraso", typeof(TimeSpan), typeof(EntradaEscalonada), new PropertyMetadata(TimeSpan.Zero));

    public static bool GetGrupo(DependencyObject d) => (bool)d.GetValue(GrupoProperty);
    public static void SetGrupo(DependencyObject d, bool value) => d.SetValue(GrupoProperty, value);
    public static bool GetCard(DependencyObject d) => (bool)d.GetValue(CardProperty);
    public static void SetCard(DependencyObject d, bool value) => d.SetValue(CardProperty, value);

    private static bool MovimentoAtivo => SystemParameters.ClientAreaAnimation;

    private static void OnGrupoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el) return;
        el.Loaded -= GrupoLoaded;
        el.IsVisibleChanged -= VisibilidadeGrupoMudou;
        el.Unloaded -= GrupoUnloaded;
        if ((bool)e.NewValue)
        {
            el.Loaded += GrupoLoaded;
            el.IsVisibleChanged += VisibilidadeGrupoMudou;
            el.Unloaded += GrupoUnloaded;
        }
    }

    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el) return;
        el.Loaded -= CardLoaded;
        el.IsVisibleChanged -= VisibilidadeCardMudou;
        if ((bool)e.NewValue)
        {
            el.Loaded += CardLoaded;
            el.IsVisibleChanged += VisibilidadeCardMudou;
        }
    }

    private static void GrupoUnloaded(object sender, RoutedEventArgs e) =>
        ((DependencyObject)sender).ClearValue(InicioProperty);

    /// <summary>
    /// Trocar de aba na barra lateral só esconde a tela (os containers continuam carregados),
    /// então a entrada é reexecutada quando ela reaparece. Ao esconder, zera a rodada da seção.
    /// </summary>
    private static void VisibilidadeGrupoMudou(object sender, DependencyPropertyChangedEventArgs e)
    {
        var grupo = (FrameworkElement)sender;
        if (!(bool)e.NewValue)
        {
            grupo.ClearValue(InicioProperty);
            return;
        }
        if (grupo.IsLoaded)
            GrupoLoaded(grupo, new RoutedEventArgs());
    }

    private static void VisibilidadeCardMudou(object sender, DependencyPropertyChangedEventArgs e)
    {
        var card = (FrameworkElement)sender;
        if ((bool)e.NewValue && card.IsLoaded)
            CardLoaded(card, new RoutedEventArgs());
    }

    private static void GrupoLoaded(object sender, RoutedEventArgs e)
    {
        var grupo = (FrameworkElement)sender;
        var atraso = ObterAtrasoGrupo(grupo);
        if (!MovimentoAtivo) return;

        var sb = new Storyboard { FillBehavior = FillBehavior.Stop };
        sb.Children.Add(Animar("Opacity", atraso, DuracaoGrupo, 0, 1, new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        sb.Children.Add(Animar("(FrameworkElement.LayoutTransform).(ScaleTransform.ScaleY)", atraso, DuracaoGrupo, 0, 1,
            new CubicEase { EasingMode = EasingMode.EaseOut }));
        sb.Begin(grupo, HandoffBehavior.SnapshotAndReplace);
    }

    private static void CardLoaded(object sender, RoutedEventArgs e)
    {
        var card = (FrameworkElement)sender;
        if (!MovimentoAtivo) return;

        var indice = ItemsControl.ItemsControlFromItemContainer(card)?.ItemContainerGenerator.IndexFromContainer(card) ?? 0;
        var escalonamento = IntervaloCards * Math.Clamp(indice, 0, MaxCardsEscalonados);
        var atraso = escalonamento;

        if (EncontrarGrupo(card) is { } grupo)
        {
            // Espera a seção abrir: o atraso conta a partir do instante em que ela entrou.
            var alvo = ObterAtrasoGrupo(grupo)
                       + TimeSpan.FromMilliseconds(DuracaoGrupo.TotalMilliseconds * 0.35)
                       + escalonamento;
            atraso = alvo - Stopwatch.GetElapsedTime((long)grupo.GetValue(InicioProperty));
            if (atraso < TimeSpan.Zero) atraso = TimeSpan.Zero;
        }

        var sb = new Storyboard { FillBehavior = FillBehavior.Stop };
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        sb.Children.Add(Animar("Opacity", atraso, DuracaoCard, 0, 1, new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        sb.Children.Add(Animar("(UIElement.RenderTransform).(ScaleTransform.ScaleX)", atraso, DuracaoCard, 0.6, 1, ease));
        sb.Children.Add(Animar("(UIElement.RenderTransform).(ScaleTransform.ScaleY)", atraso, DuracaoCard, 0.6, 1, ease));
        sb.Begin(card, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>Registra a seção na rodada atual na primeira vez que é consultada após entrar.</summary>
    private static TimeSpan ObterAtrasoGrupo(FrameworkElement grupo)
    {
        if ((long)grupo.GetValue(InicioProperty) != 0)
            return (TimeSpan)grupo.GetValue(AtrasoProperty);

        var agora = Stopwatch.GetTimestamp();
        if (_ultimoRegistro == 0 || Stopwatch.GetElapsedTime(_ultimoRegistro, agora) > JanelaRodada)
            _contagemRodada = 0;
        _ultimoRegistro = agora;

        var atraso = IntervaloGrupos * Math.Min(_contagemRodada++, MaxGruposEscalonados);
        grupo.SetValue(InicioProperty, agora);
        grupo.SetValue(AtrasoProperty, atraso);
        return atraso;
    }

    private static FrameworkElement? EncontrarGrupo(DependencyObject elemento)
    {
        for (var atual = VisualTreeHelper.GetParent(elemento); atual is not null; atual = VisualTreeHelper.GetParent(atual))
        {
            if (atual is FrameworkElement fe && GetGrupo(fe)) return fe;
        }
        return null;
    }

    /// <summary>Segura o valor inicial durante o atraso e anima até o final; não retém valor ao terminar.</summary>
    private static DoubleAnimationUsingKeyFrames Animar(string propriedade, TimeSpan atraso, TimeSpan duracao,
        double de, double para, IEasingFunction ease)
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(de, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (atraso > TimeSpan.Zero)
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(de, KeyTime.FromTimeSpan(atraso)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(para, KeyTime.FromTimeSpan(atraso + duracao), ease));
        Storyboard.SetTargetProperty(anim, new PropertyPath(propriedade));
        return anim;
    }
}
