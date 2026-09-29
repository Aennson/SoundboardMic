using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SoundboardMic.App.ViewModels;

namespace SoundboardMic.App.Views;

public partial class SoundboardView : UserControl
{
    private const string DragFormat = "SoundboardMic.AudioItem";

    private Point? _dragStart;
    private Border? _dragCard;
    private Border? _dropTarget;

    public SoundboardView() => InitializeComponent();

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Cliques nos botões do card (tocar, loop, parar, editar) não iniciam arraste.
        if (IsInsideButton(e.OriginalSource as DependencyObject, (DependencyObject)sender))
        {
            _dragStart = null;
            _dragCard = null;
            return;
        }

        _dragStart = e.GetPosition(this);
        _dragCard = (Border)sender;
    }

    private void Card_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || _dragCard is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var delta = e.GetPosition(this) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var card = _dragCard;
        _dragStart = null;
        _dragCard = null;
        if (card.DataContext is not AudioItemViewModel item) return;

        card.Opacity = 0.45;
        try
        {
            DragDrop.DoDragDrop(card, new DataObject(DragFormat, item), DragDropEffects.Move);
        }
        finally
        {
            card.ClearValue(OpacityProperty);
            ClearDropTarget();
        }
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        var target = (Border)sender;
        var ok = TryGetDrop(e, target, out _, out _);
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;

        if (!ok)
        {
            ClearDropTarget();
            return;
        }
        if (ReferenceEquals(_dropTarget, target)) return;

        ClearDropTarget();
        _dropTarget = target;
        target.BorderBrush = (Brush)FindResource("AccentBrush");
        target.BorderThickness = new Thickness(2);
    }

    private void Card_DragLeave(object sender, DragEventArgs e)
    {
        if (ReferenceEquals(_dropTarget, sender)) ClearDropTarget();
    }

    private async void Card_Drop(object sender, DragEventArgs e)
    {
        ClearDropTarget();
        e.Handled = true;
        if (!TryGetDrop(e, (Border)sender, out var origem, out var destino)) return;
        await ViewModel!.MoverAudioParaAsync(origem!, destino!);
    }

    private bool TryGetDrop(DragEventArgs e, Border target, out AudioItemViewModel? origem, out AudioItemViewModel? destino)
    {
        origem = e.Data.GetDataPresent(DragFormat) ? e.Data.GetData(DragFormat) as AudioItemViewModel : null;
        destino = target.DataContext as AudioItemViewModel;
        return origem is not null && destino is not null && ViewModel?.PodeMoverAudio(origem, destino) == true;
    }

    private void ClearDropTarget()
    {
        if (_dropTarget is null) return;
        _dropTarget.ClearValue(Border.BorderBrushProperty);
        _dropTarget.ClearValue(Border.BorderThicknessProperty);
        _dropTarget = null;
    }

    private static bool IsInsideButton(DependencyObject? source, DependencyObject card)
    {
        for (var node = source; node is not null && !ReferenceEquals(node, card);
             node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node is ButtonBase) return true;
        }
        return false;
    }
}
