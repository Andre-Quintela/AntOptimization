using AntOptimization.Domain.DTOs.Dataset;

namespace AntOptimization.Domain.Interfaces;

public enum DatasetCommandResult
{
    Ok,
    NotFound,
    InvalidState
}

/// <summary>Creation, querying and control of dataset generation jobs.</summary>
public interface IDatasetGenerationService
{
    DatasetEstimateResponse Estimate(CreateDatasetJobRequest request);
    /// <summary>Validates, persists and enqueues a job. Returns the estimate errors when invalid.</summary>
    Task<(DatasetJobDto? Job, DatasetEstimateResponse Estimate)> CreateJobAsync(CreateDatasetJobRequest request, CancellationToken cancellationToken = default);

    Task<List<DatasetJobDto>> ListJobsAsync(CancellationToken cancellationToken = default);
    Task<DatasetJobDto?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<List<DatasetInstanceDto>?> GetInstancesAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<List<AcoRunDto>?> GetRunsAsync(Guid jobId, Guid instanceId, CancellationToken cancellationToken = default);
    Task<List<ConfigSummaryDto>?> GetConfigSummariesAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<DatasetCommandResult> CancelAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<DatasetCommandResult> ResumeAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<DatasetCommandResult> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default);
}
