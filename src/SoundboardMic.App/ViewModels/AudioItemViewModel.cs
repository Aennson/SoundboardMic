using CommunityToolkit.Mvvm.ComponentModel;
using SoundboardMic.Core.Models;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// Uma linha da lista principal: um áudio e (opcionalmente) seu atalho ativo.
/// </summary>
public partial class AudioItemViewModel : ObservableObject
{
    public Audio Audio { get; private set; }
    public Mapeamento? Mapeamento { get; private set; }

    public AudioItemViewModel(Audio audio, Mapeamento? mapeamento)
    {
        Audio = audio;
        Mapeamento = mapeamento;
        _ativo = mapeamento?.Ativo ?? true;
    }

    /// <summary>
    /// Reaproveita este card com os dados recarregados do banco. Trocar o modelo em vez
    /// de recriar o view model mantém o container da grade vivo, então só o card editado
    /// anima — o resto da lista fica parado.
    /// </summary>
    public void Atualizar(Audio audio, Mapeamento? mapeamento)
    {
        Audio = audio;
        Mapeamento = mapeamento;
        Ativo = mapeamento?.Ativo ?? true;

        OnPropertyChanged(nameof(Audio));
        OnPropertyChanged(nameof(Mapeamento));
        OnPropertyChanged(nameof(Nome));
        OnPropertyChanged(nameof(CaminhoArquivo));
        OnPropertyChanged(nameof(NomeArquivo));
        OnPropertyChanged(nameof(Icone));
        OnPropertyChanged(nameof(Cor));
        OnPropertyChanged(nameof(Teclas));
        OnPropertyChanged(nameof(TemAtalho));
        OnPropertyChanged(nameof(DuracaoTexto));
        OnPropertyChanged(nameof(VolumeTexto));
    }

    public long Id => Audio.Id;

    /// <summary>True enquanto o card anima a saída após ser excluído.</summary>
    [ObservableProperty] private bool _removendo;

    /// <summary>True enquanto o card reanima a entrada após ser editado e salvo.</summary>
    [ObservableProperty] private bool _entrando;
    public string Nome => Audio.Nome;
    public string CaminhoArquivo => Audio.CaminhoArquivo;
    public string NomeArquivo => System.IO.Path.GetFileName(Audio.CaminhoArquivo);

    /// <summary>Code-point hex do glifo na barra rápida (null = padrão).</summary>
    public string? Icone => Audio.Icone;

    /// <summary>Cor de fundo do botão na barra rápida (null = padrão).</summary>
    public string? Cor => Audio.Cor;

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

    [ObservableProperty]
    private bool _emLoop;

    /// <summary>Nome da categoria do áudio (exibido na lista do Editor de sons).</summary>
    [ObservableProperty]
    private string _categoriaNome = "Sem categoria";
}
