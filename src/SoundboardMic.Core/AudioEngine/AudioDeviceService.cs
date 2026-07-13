using NAudio.CoreAudioApi;

namespace SoundboardMic.Core.AudioEngine;

public class AudioDeviceService : IAudioDeviceService
{
    // Nome do endpoint de saída criado pelo VB-Audio Virtual Cable.
    private const string CableInputNameFragment = "CABLE Input";

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices() => List(DataFlow.Render, AudioDeviceKind.Output);

    public IReadOnlyList<AudioDeviceInfo> GetInputDevices() => List(DataFlow.Capture, AudioDeviceKind.Input);

    public AudioDeviceInfo? FindCableInput() =>
        GetOutputDevices().FirstOrDefault(d =>
            d.Nome.Contains(CableInputNameFragment, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<AudioDeviceInfo> List(DataFlow flow, AudioDeviceKind kind)
    {
        using var enumerator = new MMDeviceEnumerator();

        string? defaultId = null;
        if (enumerator.HasDefaultAudioEndpoint(flow, Role.Multimedia))
        {
            using var padrao = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            defaultId = padrao.ID;
        }

        var result = new List<AudioDeviceInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device)
            {
                result.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, kind, device.ID == defaultId));
            }
        }

        // Dispositivo padrão primeiro, depois ordem alfabética.
        return result
            .OrderByDescending(d => d.PadraoDoSistema)
            .ThenBy(d => d.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Resolve um MMDevice pelo ID; null cai no dispositivo padrão do fluxo.
    /// Lança <see cref="AudioDeviceNotFoundException"/> se o ID não existir mais
    /// (ex.: dispositivo desconectado).
    /// </summary>
    internal static MMDevice ResolveDevice(string? deviceId, DataFlow flow)
    {
        var enumerator = new MMDeviceEnumerator();
        try
        {
            if (string.IsNullOrEmpty(deviceId))
                return enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);

            var device = enumerator.GetDevice(deviceId);
            if (device is null || device.State != DeviceState.Active)
                throw new AudioDeviceNotFoundException(deviceId);
            return device;
        }
        catch (Exception ex) when (ex is not AudioDeviceNotFoundException)
        {
            throw new AudioDeviceNotFoundException(deviceId ?? "(padrão)", ex);
        }
        finally
        {
            enumerator.Dispose();
        }
    }
}
