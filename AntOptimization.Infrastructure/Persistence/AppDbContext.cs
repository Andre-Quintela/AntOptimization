using AntOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntOptimization.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<OptimizationRun> OptimizationRuns => Set<OptimizationRun>();
    public DbSet<CompareRun> CompareRuns => Set<CompareRun>();
    public DbSet<CompareRunResult> CompareRunResults => Set<CompareRunResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
