using SoundboardMic.Core.Models;

namespace SoundboardMic.Core.Repositories;

public interface ICategoriaRepository
{
    Task<Categoria> AddAsync(Categoria categoria, CancellationToken ct = default);
    Task<IReadOnlyList<Categoria>> GetAllAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(Categoria categoria, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
