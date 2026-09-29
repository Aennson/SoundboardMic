using System.IO;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.App.Services;

/// <summary>
/// Persiste o resultado de um corte feito no Editor de sons: cria um áudio novo logo após
/// o original ou substitui o conteúdo do original. O áudio cortado já chega pronto (WAV);
/// aqui só se cuida de banco + cache de forma que uma falha nunca estrague o original.
/// </summary>
public class AudioTrimService
{
    private readonly IAudioRepository _audios;
    private readonly AudioFileCache _cache;

    public AudioTrimService(IAudioRepository audios, AudioFileCache cache)
    {
        _audios = audios;
        _cache = cache;
    }

    /// <summary>
    /// Cria um áudio novo com o trecho cortado, herdando categoria, volume, ícone e cor do
    /// original (sem o atalho) e posicionado logo depois dele na mesma categoria.
    /// </summary>
    public async Task<Audio> CriarNovoAsync(Audio original, byte[] wav, string nome, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        var nomeFinal = string.IsNullOrWhiteSpace(nome) ? NomeSugerido(original.Nome) : nome.Trim();

        var (caminho, conteudo, nomeArquivo) =
            await _cache.ImportarBytesAsync(wav, NomeArquivoWav(original, nomeFinal), ct);
        try
        {
            // Abre espaço logo após o original deslocando os vizinhos da mesma categoria.
            var todos = await _audios.GetAllAsync(ct);
            foreach (var vizinho in todos.Where(a =>
                         a.CategoriaId == original.CategoriaId && a.Id != original.Id && a.Ordem > original.Ordem))
            {
                vizinho.Ordem++;
                await _audios.UpdateAsync(vizinho, ct);
            }

            var novo = new Audio
            {
                Nome = nomeFinal,
                CaminhoArquivo = caminho,
                ArquivoConteudo = conteudo,
                ArquivoNomeOriginal = nomeArquivo,
                DuracaoMs = DuracaoMs(caminho),
                VolumePadrao = original.VolumePadrao,
                Icone = original.Icone,
                Cor = original.Cor,
                CategoriaId = original.CategoriaId,
                Ordem = original.Ordem + 1,
                CriadoEm = DateTime.UtcNow,
            };
            return await _audios.AddAsync(novo, ct);
        }
        catch
        {
            _cache.RemoverCache(caminho);
            throw;
        }
    }

    /// <summary>
    /// Troca o conteúdo do original pelo trecho cortado, mantendo Id, nome, categoria, ordem,
    /// volume, ícone, cor e atalho. O cache antigo só é apagado depois que o banco confirmou.
    /// </summary>
    public async Task SubstituirAsync(Audio original, byte[] wav, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(original);

        var (caminho, conteudo, nomeArquivo) =
            await _cache.ImportarBytesAsync(wav, NomeArquivoWav(original, original.Nome), ct);

        var anterior = (original.CaminhoArquivo, original.ArquivoConteudo, original.ArquivoNomeOriginal, original.DuracaoMs);
        original.CaminhoArquivo = caminho;
        original.ArquivoConteudo = conteudo;
        original.ArquivoNomeOriginal = nomeArquivo;
        original.DuracaoMs = DuracaoMs(caminho);

        try
        {
            if (!await _audios.UpdateAsync(original, ct))
                throw new InvalidOperationException("O áudio não existe mais no banco.");
        }
        catch
        {
            (original.CaminhoArquivo, original.ArquivoConteudo, original.ArquivoNomeOriginal, original.DuracaoMs) = anterior;
            _cache.RemoverCache(caminho);
            throw;
        }

        if (!string.Equals(anterior.CaminhoArquivo, caminho, StringComparison.OrdinalIgnoreCase))
            _cache.RemoverCache(anterior.CaminhoArquivo);
    }

    /// <summary>Nome padrão do áudio criado a partir de um corte.</summary>
    public static string NomeSugerido(string nomeOriginal) => $"{nomeOriginal.Trim()} (corte)";

    private static string NomeArquivoWav(Audio original, string nome)
    {
        var baseNome = !string.IsNullOrWhiteSpace(original.ArquivoNomeOriginal)
            ? Path.GetFileNameWithoutExtension(original.ArquivoNomeOriginal)
            : nome;
        foreach (var invalido in Path.GetInvalidFileNameChars())
            baseNome = baseNome.Replace(invalido, '_');
        return $"{baseNome}.wav";
    }

    private static long DuracaoMs(string caminho)
    {
        try { return AudioFileDecoder.GetDurationMs(caminho); }
        catch { return 0; }
    }
}
