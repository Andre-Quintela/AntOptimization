namespace AntOptimization.Domain.Interfaces;

/// <summary>In-process queue of dataset jobs plus cancellation of the running job.</summary>
public interface IDatasetJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);

    /// <summary>Creates a cancellation token for a job that is about to start running.</summary>
    CancellationToken BeginRun(Guid jobId, CancellationToken stoppingToken);
    void EndRun(Guid jobId);
    /// <summary>Requests cancellation of a running job. Returns false when the job is not running.</summary>
    bool RequestCancel(Guid jobId);
    bool IsRunning(Guid jobId);
}
