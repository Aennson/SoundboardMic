using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.App.ViewModels;

/// <summary>Item de combo para um dispositivo (ou a opção "padrão do sistema").</summary>
public class DeviceOption
{
    public string? Id { get; }
    public string Nome { get; }

    public DeviceOption(string? id, string nome)
    {
        Id = id;
        Nome = nome;
    }

    public static DeviceOption Default(string sufixo) => new(null, $"Padrão do sistema ({sufixo})");

    public static DeviceOption From(AudioDeviceInfo info) =>
        new(info.Id, info.PadraoDoSistema ? $"{info.Nome}  •  padrão" : info.Nome);

    public override string ToString() => Nome;
}
