using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.Entities;

namespace AntOptimization.Domain.DTOs.Dataset;

public class RegionDto
{
    public double South { get; set; }
    public double West { get; set; }
    public double North { get; set; }
    public double East { get; set; }

    public GeoBounds ToBounds() => new(South, West, North, East);
}

public class CreateDatasetJobRequest
{
    public string? Name { get; set; }
    public RegionDto Region { get; set; } = new();
    /// <summary>Instance sizes to cover; one point set is drawn per size per replica.</summary>
    public List<int> PointCounts { get; set; } = [8];
    /// <summary>Independent point sets drawn for each size in <see cref="PointCounts"/>.</summary>
    public int InstancesPerPointCount { get; set; } = 10;
    public DistanceSource DistanceSource { get; set; } = DistanceSource.Osrm;
    public bool FixedStart { get; set; } = true;
    public int Repetitions { get; set; } = 3;
    /// <summary>Optional; a random seed is drawn and stored when omitted.</summary>
    public int? MasterSeed { get; set; }
    public HyperparameterGrid Grid { get; set; } = new();
}

public class DatasetEstimateResponse
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int ConfigurationCount { get; set; }
    /// <summary>Normalized (deduplicated, ascending) sizes the job will generate.</summary>
    public List<int> PointCounts { get; set; } = [];
    /// <summary>Sizes × instances per size.</summary>
    public int TotalInstances { get; set; }
    public int TotalRuns { get; set; }
    public string? ExactMethod { get; set; }
    /// <summary>Worst case: operations for the largest size.</summary>
    public double ExactOperationsPerInstance { get; set; }
    public double ExactMemoryBytes { get; set; }
    public double EstimatedAcoSeconds { get; set; }
    public double EstimatedExactSeconds { get; set; }
    public double EstimatedNetworkSeconds { get; set; }
    public double EstimatedTotalSeconds { get; set; }
}

public class DatasetJobDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DatasetJobStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public RegionDto Region { get; set; } = new();
    public List<int> PointCounts { get; set; } = [];
    public int InstancesPerPointCount { get; set; }
    public int InstanceCount { get; set; }
    public DistanceSource DistanceSource { get; set; }
    public bool FixedStart { get; set; }
    public int Repetitions { get; set; }
    public int MasterSeed { get; set; }
    public HyperparameterGrid Grid { get; set; } = new();
    public int ConfigurationCount { get; set; }
    public int TotalRuns { get; set; }
    public int CompletedRuns { get; set; }
    public int ProcessedInstances { get; set; }
    public int FailedInstances { get; set; }
    public double ProgressPercent { get; set; }
}

public class DatasetInstanceDto
{
    public Guid Id { get; set; }
    public int Index { get; set; }
    public int PointCount { get; set; }
    public int Seed { get; set; }
    public DatasetInstanceStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int GenerationAttempts { get; set; }
    public List<LocationDto> Coordinates { get; set; } = [];
    public int? FixedStartIndex { get; set; }
    public InstanceFeatures? Features { get; set; }
    public double? OptimalDistanceM { get; set; }
    public List<int> OptimalTour { get; set; } = [];
    public string? ExactMethod { get; set; }
    public double? ExactTimeMs { get; set; }
    public long? ExactOperations { get; set; }
    public int RunCount { get; set; }
    public double? BestGapPercent { get; set; }
    public double? MeanGapPercent { get; set; }
}

public class AcoRunDto
{
    public Guid Id { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public int Repetition { get; set; }
    public int Seed { get; set; }
    public AcoRunStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public double Alpha { get; set; }
    public double Beta { get; set; }
    public int NumberOfAnts { get; set; }
    public double EvaporationRate { get; set; }
    public int Iterations { get; set; }
    public double BestDistanceM { get; set; }
    public double GapAbsM { get; set; }
    public double GapPercent { get; set; }
    public bool ReachedOptimum { get; set; }
    public int BestIteration { get; set; }
    public int? FirstOptimalIteration { get; set; }
    public long ToursConstructed { get; set; }
    public double ExecutionTimeMs { get; set; }
    public List<int> BestTour { get; set; } = [];
}

/// <summary>Aggregated performance of one hyperparameter configuration across the job's instances.</summary>
public class ConfigSummaryDto
{
    public string ConfigKey { get; set; } = string.Empty;
    public double Alpha { get; set; }
    public double Beta { get; set; }
    public int NumberOfAnts { get; set; }
    public double EvaporationRate { get; set; }
    public int Iterations { get; set; }
    public int RunCount { get; set; }
    public int InstanceCount { get; set; }
    public double MeanGapPercent { get; set; }
    public double StdGapPercent { get; set; }
    public double MinGapPercent { get; set; }
    public double MaxGapPercent { get; set; }
    /// <summary>Fraction (0–1) of runs that reached the optimum.</summary>
    public double OptimalHitRate { get; set; }
    public double MeanExecutionTimeMs { get; set; }
    public double MeanBestIteration { get; set; }
    /// <summary>Number of instances in which this configuration had the lowest mean gap (ties count).</summary>
    public int InstancesWon { get; set; }
}
