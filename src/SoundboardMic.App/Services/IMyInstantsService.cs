namespace SoundboardMic.App.Services;

/// <summary>
/// Integração com o myinstants.com: lista os sons "bombando no Brasil" ou os
/// resultados de uma busca, e baixa o áudio de um som escolhido para importação.
/// Todo o conteúdo pertence e é fornecido pelo myinstants.com — a atribuição
/// deve ser mantida visível na UI que consome este serviço.
/// </summary>
public interface IMyInstantsService
{
    /// <summary>
    /// Busca sons no myinstants.com. Sem termo (ou em branco), retorna os sons em
    /// alta no Brasil; com termo, retorna os resultados da busca por nome.
    /// </summary>
    Task<IReadOnlyList<MyInstantsSound>> BuscarAsync(string? termo, CancellationToken ct = default);

    /// <summary>Baixa o conteúdo binário (.mp3) de um som do myinstants.com.</summary>
    Task<byte[]> BaixarAudioAsync(MyInstantsSound som, CancellationToken ct = default);
}
