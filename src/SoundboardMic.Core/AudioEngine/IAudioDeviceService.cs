namespace SoundboardMic.Core.AudioEngine;

public interface IAudioDeviceService
{
    /// <summary>Dispositivos de saída (render) ativos.</summary>
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices();

    /// <summary>Dispositivos de entrada (capture) ativos.</summary>
    IReadOnlyList<AudioDeviceInfo> GetInputDevices();

    /// <summary>
    /// Localiza o dispositivo de saída do VB-Audio Virtual Cable ("CABLE Input").
    /// Retorna null se o VB-Cable não estiver instalado.
    /// </summary>
    AudioDeviceInfo? FindCableInput();
}
