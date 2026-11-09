namespace AntOptimization.Domain.Entities;

public enum DatasetJobStatus
{
    Pending,
    Running,
    Completed,
    CompletedWithErrors,
    Failed,
    Cancelled,
    Interrupted
}

public enum DistanceSource
{
    Osrm,
    Haversine
}

/// <summary>
/// One dataset generation request: a region, the instance generation settings and the
/// hyperparameter grid evaluated on every instance.
/// </summary>
public class DatasetJob
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DatasetJobStatus Status { get; set; } = DatasetJobStatus.Pending;
    public string? ErrorMessage { get; set; }

    // Region
    public double South { get; set; }
    public double West { get; set; }
    public double North { get; set; }
    public double East { get; set; }

    // Generation settings
    /// <summary>Serialized ascending list of instance sizes (point counts) covered by this job.</summary>
    public string PointCountsJson { get; set; } = "[]";
    /// <summary>How many independent point sets are drawn for each size in <see cref="PointCountsJson"/>.</summary>
    public int InstancesPerPointCount { get; set; }
    /// <summary>Total instances: sizes × <see cref="InstancesPerPointCount"/>.</summary>
    public int InstanceCount { get; set; }
    public DistanceSource DistanceSource { get; set; }
    /// <summary>When true every tour starts at point 0 (ACO and exact solver alike).</summary>
    public bool FixedStart { get; set; }
    public int Repetitions { get; set; }
    public int MasterSeed { get; set; }
    /// <summary>Serialized <see cref="Dataset.HyperparameterGrid"/>.</summary>
    public string GridJson { get; set; } = "{}";
    public int ConfigurationCount { get; set; }

    // Progress
    public int TotalRuns { get; set; }
    public int CompletedRuns { get; set; }
    public int ProcessedInstances { get; set; }
    public int FailedInstances { get; set; }

    public List<DatasetInstance> Instances { get; set; } = [];

    public bool IsActive => Status is DatasetJobStatus.Pending or DatasetJobStatus.Running;
}
