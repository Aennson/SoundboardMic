using SoundboardMic.Core.Models;

namespace SoundboardMic.Core.Repositories;

public interface IMapeamentoRepository
{
    Task<Mapeamento> AddAsync(Mapeamento mapeamento, CancellationToken ct = default);
    Task<Mapeamento?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Mapeamento?> GetByTeclasAsync(string teclas, CancellationToken ct = default);
    Task<IReadOnlyList<Mapeamento>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Mapeamento>> GetByAudioIdAsync(long audioId, CancellationToken ct = default);
    Task<bool> UpdateAsync(Mapeamento mapeamento, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
