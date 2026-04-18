using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AntOptimization.Infrastructure.Persistence.Repositories;

public class OptimizationRunRepository : IOptimizationRunRepository
{
    private readonly AppDbContext _context;

    public OptimizationRunRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(OptimizationRun run, CancellationToken cancellationToken = default)
    {
        _context.OptimizationRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OptimizationRun>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.OptimizationRuns
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<OptimizationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.OptimizationRuns.FindAsync([id], cancellationToken);
}
