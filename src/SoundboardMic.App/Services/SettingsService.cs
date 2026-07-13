using System.IO;
using System.Text.Json;

namespace SoundboardMic.App.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
}

/// <summary>Carrega/salva <see cref="AppSettings"/> em JSON no diretório do app.</summary>
public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoundboardMic");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch
        {
            // Arquivo corrompido: recomeça com padrões (não deve travar o app).
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch
        {
            // Falha ao gravar não é fatal para o uso corrente.
        }
    }
}
