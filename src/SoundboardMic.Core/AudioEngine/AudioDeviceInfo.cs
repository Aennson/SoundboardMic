namespace SoundboardMic.Core.AudioEngine;

/// <summary>Tipo do dispositivo de áudio.</summary>
public enum AudioDeviceKind
{
    /// <summary>Saída (alto-falantes, fones, CABLE Input).</summary>
    Output,

    /// <summary>Entrada (microfones, CABLE Output).</summary>
    Input,
}

/// <summary>
/// Descrição imutável de um dispositivo de áudio WASAPI.
/// </summary>
/// <param name="Id">ID do endpoint MMDevice (estável entre sessões).</param>
/// <param name="Nome">Nome amigável exibido ao usuário.</param>
/// <param name="Kind">Entrada ou saída.</param>
/// <param name="PadraoDoSistema">Se é o dispositivo padrão do Windows.</param>
public record AudioDeviceInfo(string Id, string Nome, AudioDeviceKind Kind, bool PadraoDoSistema);
