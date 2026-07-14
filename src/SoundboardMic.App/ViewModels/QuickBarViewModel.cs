using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardMic.App.Services;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// Estado da barra rápida flutuante: lista de áudios (compartilhada com a
/// janela principal), orientação, disparo, pânico e persistência de posição.
/// </summary>
public partial class QuickBarViewModel : ObservableObject
{
    private readonly SoundboardController _controller;
    private readonly ISettingsService _settings;
    private readonly QuickBarService _quickBar;

    public QuickBarViewModel(
        MainViewModel mainViewModel,
        SoundboardController controller,
        ISettingsService settings,
        QuickBarService quickBar)
    {
        _controller = controller;
        _settings = settings;
        _quickBar = quickBar;

        // View própria sobre a MESMA coleção do MainViewModel: reflete
        // add/edit/delete e não herda o filtro de busca da janela principal
        // (que atua na default view).
        AudiosView = new ListCollectionView(mainViewModel.Audios);

        _horizontal = !string.Equals(
            settings.Current.QuickBarOrientation, "Vertical", StringComparison.OrdinalIgnoreCase);
    }

    public ICollectionView AudiosView { get; }

    [ObservableProperty] private bool _horizontal;

    /// <summary>Lado dos botões, calculado pela janela a partir da altura da taskbar.</summary>
    [ObservableProperty] private double _tamanhoBotao = 38;

    public Orientation Orientacao => Horizontal ? Orientation.Horizontal : Orientation.Vertical;

    partial void OnHorizontalChanged(bool value)
    {
        OnPropertyChanged(nameof(Orientacao));
        _settings.Current.QuickBarOrientation = value ? "Horizontal" : "Vertical";
        _settings.Save();
    }

    [RelayCommand]
    private void Tocar(AudioItemViewModel? item)
    {
        if (item is null) return;
        _controller.TriggerSound(item.CaminhoArquivo, (float)item.Audio.VolumePadrao);
    }

    [RelayCommand]
    private void Panico() => _controller.StopAllSounds();

    [RelayCommand]
    private void ModoHorizontal() => Horizontal = true;

    [RelayCommand]
    private void ModoVertical() => Horizontal = false;

    [RelayCommand]
    private void Ocultar() => _quickBar.Ocultar();

    [RelayCommand]
    private void AbrirJanelaPrincipal()
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.RestoreFromTray();
    }

    public void SalvarPosicao(double left, double top)
    {
        _settings.Current.QuickBarLeft = left;
        _settings.Current.QuickBarTop = top;
        _settings.Save();
    }
}
