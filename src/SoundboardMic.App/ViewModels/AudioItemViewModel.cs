using CommunityToolkit.Mvvm.ComponentModel;
using SoundboardMic.Core.Models;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// Uma linha da lista principal: um áudio e (opcionalmente) seu atalho ativo.
/// </summary>
public partial class AudioItemViewModel : ObservableObject
{
    public Audio Audio { get; }
    public Mapeamento? Mapeamento { get; }

    public AudioItemViewModel(Audio audio, Mapeamento? mapeamento)
    {
        Audio = audio;
        Mapeamento = mapeamento;
        _ativo = mapeamento?.Ativo ?? true;
    }

    public long Id => Audio.Id;
    public string Nome => Audio.Nome;
    public string CaminhoArquivo => Audio.CaminhoArquivo;
    public string NomeArquivo => System.IO.Path.GetFileName(Audio.CaminhoArquivo);

    /// <summary>Atalho legível ou vazio (a UI mostra um chip cinza "sem atalho").</summary>
    public string Teclas => Mapeamento?.Teclas ?? string.Empty;

    public bool TemAtalho => !string.IsNullOrEmpty(Mapeamento?.Teclas);

    public string DuracaoTexto
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(Audio.DuracaoMs);
            return Audio.DuracaoMs <= 0 ? "--:--" : $"{(int)ts.TotalMinutes:0}:{ts.Seconds:00}";
        }
    }

    public string VolumeTexto => $"{Audio.VolumePadrao * 100:0}%";

    /// <summary>Estado do toggle ativo/inativo (persistido pelo comando do MainViewModel).</summary>
    [ObservableProperty]
    private bool _ativo;

    /// <summary>Destaca o card enquanto o som está tocando.</summary>
    [ObservableProperty]
    private bool _tocando;
}
