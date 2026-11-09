using AntOptimization.Domain.Entities;

namespace AntOptimization.Domain.Interfaces;

public record InstanceRunStats(int RunCount, double? BestGapPercent, double? MeanGapPercent);

/// <summary>
/// Persistence of dataset jobs, instances and ACO runs. Returned entities are detached
/// (not tracked); updates are explicit.
/// </summary>
public interface IDatasetRepository
{
    /// <summary>Stores a job together with its (pending) instances.</summary>
    Task AddJobAsync(DatasetJob job, CancellationToken cancellationToken = default);
    Task<DatasetJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<List<DatasetJob>> ListJobsAsync(CancellationToken cancellationToken = default);
    /// <summary>Updates the job row only (status, progress), never its instances.</summary>
    Task UpdateJobAsync(DatasetJob job, CancellationToken cancellationToken = default);
    Task<bool> DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    /// <summary>Marks jobs left in Running state (e.g. after a restart) as Interrupted.</summary>
    Task<int> MarkRunningJobsInterruptedAsync(CancellationToken cancellationToken = default);

    Task<List<DatasetInstance>> GetInstancesAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<DatasetInstance?> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, InstanceRunStats>> GetInstanceRunStatsAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically stores an instance's final state and replaces all of its runs,
    /// so an instance never ends up with a partial set of runs.
    /// </summary>
    Task SaveInstanceResultAsync(DatasetInstance instance, IReadOnlyList<AcoRun> runs, CancellationToken cancellationToken = default);

    Task<List<AcoRun>> GetRunsAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<List<AcoRun>> GetRunsForJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Streams completed instances with their runs, for export. All jobs when <paramref name="jobId"/> is null.</summary>
    IAsyncEnumerable<DatasetInstance> StreamInstancesWithRunsAsync(Guid? jobId, CancellationToken cancellationToken = default);
}
