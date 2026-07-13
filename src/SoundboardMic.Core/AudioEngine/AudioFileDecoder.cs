using NAudio.Vorbis;
using NAudio.Wave;

namespace SoundboardMic.Core.AudioEngine;

/// <summary>
/// Abre arquivos de áudio suportados (.mp3, .wav, .ogg) como stream decodificado.
/// </summary>
public static class AudioFileDecoder
{
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".ogg" };

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Abre o arquivo para leitura decodificada. O retorno também implementa
    /// <see cref="ISampleProvider"/> (float 32-bit) nos dois readers usados.
    /// </summary>
    /// <exception cref="FileNotFoundException">Arquivo movido ou deletado.</exception>
    /// <exception cref="FormatoNaoSuportadoException">Extensão não suportada ou arquivo corrompido.</exception>
    public static WaveStream OpenRead(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Arquivo de áudio não encontrado: {path}", path);

        if (!IsSupported(path))
            throw new FormatoNaoSuportadoException(path);

        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".ogg" => new VorbisWaveReader(path),
                _ => new AudioFileReader(path),
            };
        }
        catch (Exception ex) when (ex is not FormatoNaoSuportadoException)
        {
            // Extensão certa mas conteúdo inválido/corrompido, ou codec ausente.
            throw new FormatoNaoSuportadoException(path, ex);
        }
    }

    /// <summary>Converte o stream aberto em ISampleProvider (float 32-bit).</summary>
    public static ISampleProvider ToSampleProvider(WaveStream stream) => stream switch
    {
        ISampleProvider sp => sp,
        _ => stream.ToSampleProvider(),
    };

    /// <summary>Duração do arquivo em milissegundos (probe rápido).</summary>
    public static long GetDurationMs(string path)
    {
        using var reader = OpenRead(path);
        return (long)reader.TotalTime.TotalMilliseconds;
    }
}
