using System.Runtime.CompilerServices;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AntOptimization.Infrastructure.Persistence;

public class DatasetRepository : IDatasetRepository
{
    private readonly AntOptimizationDbContext _db;

    public DatasetRepository(AntOptimizationDbContext db)
    {
        _db = db;
    }

    public async Task AddJobAsync(DatasetJob job, CancellationToken cancellationToken = default)
    {
        _db.DatasetJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();
    }

    public Task<DatasetJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _db.DatasetJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

    public async Task<List<DatasetJob>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _db.DatasetJobs.AsNoTracking().ToListAsync(cancellationToken);
        return jobs.OrderByDescending(j => j.CreatedAt).ToList();
    }

    public async Task UpdateJobAsync(DatasetJob job, CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        _db.Entry(job).State = EntityState.Modified;
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();
    }

    public async Task<bool> DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // Explicit bottom-up deletes: fast and independent of SQLite foreign-key enforcement.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var instanceIds = _db.DatasetInstances.Where(i => i.JobId == jobId).Select(i => i.Id);
        await _db.AcoRuns.Where(r => instanceIds.Contains(r.InstanceId)).ExecuteDeleteAsync(cancellationToken);
        await _db.DatasetInstances.Where(i => i.JobId == jobId).ExecuteDeleteAsync(cancellationToken);
        int deleted = await _db.DatasetJobs.Where(j => j.Id == jobId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted > 0;
    }

    public Task<int> MarkRunningJobsInterruptedAsync(CancellationToken cancellationToken = default) =>
        _db.DatasetJobs
            .Where(j => j.Status == DatasetJobStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, DatasetJobStatus.Interrupted)
                .SetProperty(j => j.ErrorMessage, "Processamento interrompido (reinício do servidor). Use \"Retomar\" para continuar."),
                cancellationToken);

    public Task<List<DatasetInstance>> GetInstancesAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _db.DatasetInstances.AsNoTracking()
            .Where(i => i.JobId == jobId)
            .OrderBy(i => i.Index)
            .ToListAsync(cancellationToken);

    public Task<DatasetInstance?> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        _db.DatasetInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId, cancellationToken);

    public async Task<Dictionary<Guid, InstanceRunStats>> GetInstanceRunStatsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var stats = await _db.AcoRuns.AsNoTracking()
            .Where(r => r.Instance!.JobId == jobId && r.Status == AcoRunStatus.Completed)
            .GroupBy(r => r.InstanceId)
            .Select(g => new
            {
                InstanceId = g.Key,
                Count = g.Count(),
                Best = g.Min(r => r.GapPercent),
                Mean = g.Average(r => r.GapPercent)
            })
            .ToListAsync(cancellationToken);

        return stats.ToDictionary(s => s.InstanceId, s => new InstanceRunStats(s.Count, s.Best, s.Mean));
    }

    public async Task SaveInstanceResultAsync(DatasetInstance instance, IReadOnlyList<AcoRun> runs, CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await _db.AcoRuns.Where(r => r.InstanceId == instance.Id).ExecuteDeleteAsync(cancellationToken);

        instance.Runs = [];
        _db.DatasetInstances.Update(instance);
        _db.AcoRuns.AddRange(runs);
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();
    }

    public Task<List<AcoRun>> GetRunsAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        _db.AcoRuns.AsNoTracking()
            .Where(r => r.InstanceId == instanceId)
            .OrderBy(r => r.ConfigKey).ThenBy(r => r.Repetition)
            .ToListAsync(cancellationToken);

    public Task<List<AcoRun>> GetRunsForJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _db.AcoRuns.AsNoTracking()
            .Where(r => r.Instance!.JobId == jobId)
            .ToListAsync(cancellationToken);

    public async IAsyncEnumerable<DatasetInstance> StreamInstancesWithRunsAsync(
        Guid? jobId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.DatasetInstances.AsNoTracking();
        if (jobId.HasValue)
            query = query.Where(i => i.JobId == jobId.Value);

        var instanceIds = await query
            .OrderBy(i => i.JobId).ThenBy(i => i.Index)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        // One instance at a time keeps memory bounded for large datasets.
        foreach (var id in instanceIds)
        {
            var instance = await _db.DatasetInstances.AsNoTracking()
                .Include(i => i.Runs.OrderBy(r => r.ConfigKey).ThenBy(r => r.Repetition))
                .FirstAsync(i => i.Id == id, cancellationToken);
            yield return instance;
        }
    }
}
