using CommunityToolkit.Mvvm.ComponentModel;

namespace SoundboardMic.App.ViewModels;

/// <summary>Um item da lista do myinstants.com (mini card): nome, cor e estados de UI.</summary>
public partial class MyInstantsSoundViewModel : ObservableObject
{
    public Services.MyInstantsSound Som { get; }

    public MyInstantsSoundViewModel(Services.MyInstantsSound som) => Som = som;

    public string Nome => Som.Nome;
    public string? Cor => Som.CorHex;

    /// <summary>Este som está tocando agora (preview, sem ter sido adicionado).</summary>
    [ObservableProperty] private bool _tocando;

    /// <summary>Baixando o áudio para tocar ou adicionar (mostra um spinner no botão).</summary>
    [ObservableProperty] private bool _ocupado;

    /// <summary>Já foi adicionado ao acervo pessoal nesta sessão (botão vira "Adicionado").</summary>
    [ObservableProperty] private bool _adicionado;
}
