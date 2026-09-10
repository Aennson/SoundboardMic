using System.IO;
using Serilog;
using SoundboardMic.Core.Models;

namespace SoundboardMic.App.Services;

/// <summary>
/// Gerencia a cópia local (cache em disco) dos arquivos de áudio cujo conteúdo real
/// vive no banco (<see cref="Audio.ArquivoConteudo"/>). O motor de reprodução (NAudio)
/// só sabe ler de um caminho de arquivo, então mantemos uma cópia materializada em
/// disco — mas o banco é sempre a fonte de verdade: se a cópia sumir (cache limpo,
/// perfil novo, banco copiado de outra máquina), ela é regenerada automaticamente.
/// </summary>
public class AudioFileCache
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SoundboardMic", "AudioCache");

    /// <summary>Lê os bytes do arquivo escolhido pelo usuário e grava uma cópia própria no cache.</summary>
    public async Task<(string CaminhoCache, byte[] Conteudo, string NomeOriginal)> ImportarAsync(
        string caminhoOriginal, CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(caminhoOriginal, ct);
        var extensao = Path.GetExtension(caminhoOriginal);
        Directory.CreateDirectory(CacheDirectory);
        var caminhoCache = Path.Combine(CacheDirectory, $"{Guid.NewGuid():N}{extensao}");
        await File.WriteAllBytesAsync(caminhoCache, bytes, ct);
        return (caminhoCache, bytes, Path.GetFileName(caminhoOriginal));
    }

    /// <summary>
    /// Garante que o arquivo em <see cref="Audio.CaminhoArquivo"/> exista no disco,
    /// regenerando-o a partir de <see cref="Audio.ArquivoConteudo"/> se necessário.
    /// Retorna true se o caminho foi (re)criado/alterado (chamador deve persistir).
    /// </summary>
    public bool GarantirMaterializado(Audio audio)
    {
        if (audio.ArquivoConteudo is null || audio.ArquivoConteudo.Length == 0)
            return false;

        if (!string.IsNullOrEmpty(audio.CaminhoArquivo) && File.Exists(audio.CaminhoArquivo))
            return false;

        try
        {
            Directory.CreateDirectory(CacheDirectory);
            var extensao = !string.IsNullOrEmpty(audio.ArquivoNomeOriginal)
                ? Path.GetExtension(audio.ArquivoNomeOriginal)
                : (!string.IsNullOrEmpty(audio.CaminhoArquivo) ? Path.GetExtension(audio.CaminhoArquivo) : ".mp3");
            var caminho = Path.Combine(CacheDirectory, $"{Guid.NewGuid():N}{extensao}");
            File.WriteAllBytes(caminho, audio.ArquivoConteudo);
            audio.CaminhoArquivo = caminho;
            Log.Information("Cache de áudio regenerado para '{Nome}' em {Caminho}.", audio.Nome, caminho);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao regenerar cache de áudio para '{Nome}'.", audio.Nome);
            return false;
        }
    }

    /// <summary>
    /// Migra um áudio legado (sem conteúdo salvo no banco ainda) lendo o arquivo do
    /// caminho externo original e passando a guardá-lo no cache + no banco. Não faz
    /// nada se o arquivo original já não existir mais (nada a importar).
    /// </summary>
    public async Task<bool> MigrarLegadoAsync(Audio audio, CancellationToken ct = default)
    {
        if (audio.ArquivoConteudo is not null && audio.ArquivoConteudo.Length > 0)
            return false;
        if (string.IsNullOrEmpty(audio.CaminhoArquivo) || !File.Exists(audio.CaminhoArquivo))
            return false;

        try
        {
            var (caminhoCache, bytes, nomeOriginal) = await ImportarAsync(audio.CaminhoArquivo, ct);
            audio.ArquivoConteudo = bytes;
            audio.ArquivoNomeOriginal = nomeOriginal;
            audio.CaminhoArquivo = caminhoCache;
            Log.Information("Áudio legado '{Nome}' importado para o banco.", audio.Nome);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao importar áudio legado '{Nome}' para o banco.", audio.Nome);
            return false;
        }
    }

    /// <summary>Remove o arquivo em cache, se existir (best-effort, chamado após exclusão do áudio).</summary>
    public void RemoverCache(string? caminho)
    {
        if (string.IsNullOrEmpty(caminho))
            return;
        try
        {
            if (File.Exists(caminho))
                File.Delete(caminho);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha ao remover arquivo de cache {Caminho}.", caminho);
        }
    }
}
