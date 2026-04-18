using AntOptimization.Domain.Entities;

namespace AntOptimization.Domain.Interfaces;

public interface ICompareRunRepository
{
    Task AddAsync(CompareRun run, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompareRun>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CompareRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
