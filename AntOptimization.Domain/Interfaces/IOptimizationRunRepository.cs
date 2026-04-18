using AntOptimization.Domain.Entities;

namespace AntOptimization.Domain.Interfaces;

public interface IOptimizationRunRepository
{
    Task AddAsync(OptimizationRun run, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OptimizationRun>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OptimizationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
