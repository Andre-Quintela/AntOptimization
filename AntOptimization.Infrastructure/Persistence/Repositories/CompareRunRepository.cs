using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AntOptimization.Infrastructure.Persistence.Repositories;

public class CompareRunRepository : ICompareRunRepository
{
    private readonly AppDbContext _context;

    public CompareRunRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(CompareRun run, CancellationToken cancellationToken = default)
    {
        _context.CompareRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompareRun>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.CompareRuns
            .Include(x => x.Results)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<CompareRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.CompareRuns
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}
