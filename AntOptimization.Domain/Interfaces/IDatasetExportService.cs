namespace AntOptimization.Domain.Interfaces;

public enum DatasetExportLevel
{
    /// <summary>One row per ACO run with denormalized instance features (main training table).</summary>
    Runs,
    /// <summary>One row per instance with coordinates, features and optimum.</summary>
    Instances,
    /// <summary>One row per (instance, configuration) with aggregated statistics over repetitions.</summary>
    Configs,
    /// <summary>ZIP with the three CSVs plus metadata.json.</summary>
    Zip
}

public interface IDatasetExportService
{
    /// <param name="jobId">Job to export, or null for every job.</param>
    Task WriteAsync(Guid? jobId, DatasetExportLevel level, Stream output, CancellationToken cancellationToken = default);
}
