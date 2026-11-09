using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Server.BackgroundServices;

/// <summary>
/// Consumes the dataset job queue, one job at a time, so ACO execution times stay comparable.
/// On startup re-enqueues jobs that were still pending.
/// </summary>
public class DatasetGenerationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDatasetJobQueue _queue;
    private readonly ILogger<DatasetGenerationWorker> _logger;

    public DatasetGenerationWorker(IServiceScopeFactory scopeFactory, IDatasetJobQueue queue, ILogger<DatasetGenerationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingJobsAsync(stoppingToken);

        await foreach (var jobId in _queue.DequeueAllAsync(stoppingToken))
        {
            var jobToken = _queue.BeginRun(jobId, stoppingToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IDatasetJobRunner>();
                _logger.LogInformation("Dataset job {JobId} started", jobId);
                await runner.RunAsync(jobId, jobToken);
                _logger.LogInformation("Dataset job {JobId} finished", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dataset job {JobId} crashed", jobId);
            }
            finally
            {
                _queue.EndRun(jobId);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                await MarkInterruptedAsync(jobId);
                break;
            }
        }
    }

    private async Task RequeuePendingJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDatasetRepository>();
        var pending = (await repository.ListJobsAsync(stoppingToken))
            .Where(j => j.Status == DatasetJobStatus.Pending)
            .OrderBy(j => j.CreatedAt);

        foreach (var job in pending)
            await _queue.EnqueueAsync(job.Id, stoppingToken);
    }

    /// <summary>A job stopped by server shutdown is resumable, not user-cancelled.</summary>
    private async Task MarkInterruptedAsync(Guid jobId)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDatasetRepository>();
        var job = await repository.GetJobAsync(jobId);
        if (job is { Status: DatasetJobStatus.Cancelled or DatasetJobStatus.Running })
        {
            job.Status = DatasetJobStatus.Interrupted;
            job.ErrorMessage = "Processamento interrompido pelo desligamento do servidor. Use \"Retomar\" para continuar.";
            await repository.UpdateJobAsync(job);
        }
    }
}
