namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Lançada quando um dispositivo de áudio salvo nas configurações não está mais
/// disponível (desconectado, desabilitado ou driver removido).
/// </summary>
public class AudioDeviceNotFoundException : Exception
{
    public string DeviceId { get; }

    public AudioDeviceNotFoundException(string deviceId, Exception? inner = null)
        : base($"Dispositivo de áudio não encontrado ou inativo: {deviceId}", inner)
    {
        DeviceId = deviceId;
    }
}
