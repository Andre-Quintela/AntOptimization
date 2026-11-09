namespace AntOptimization.Domain.Interfaces;

/// <summary>Executes a dataset job: instances → optimum → ACO grid → persistence.</summary>
public interface IDatasetJobRunner
{
    Task RunAsync(Guid jobId, CancellationToken cancellationToken);
}
