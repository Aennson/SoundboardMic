using SoundboardMic.Core.Models;

namespace SoundboardMic.Core.Repositories;

public interface IAudioRepository
{
    Task<Audio> AddAsync(Audio audio, CancellationToken ct = default);
    Task<Audio?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Audio>> GetAllAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(Audio audio, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
