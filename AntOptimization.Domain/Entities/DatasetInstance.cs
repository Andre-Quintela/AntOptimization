using AntOptimization.Domain.Algorithms.Exact;
using AntOptimization.Domain.Dataset;

namespace AntOptimization.Domain.Entities;

public enum DatasetInstanceStatus
{
    Pending,
    /// <summary>Optimum and all ACO runs stored.</summary>
    Completed,
    /// <summary>Points/matrix generation or a solver failed.</summary>
    Failed,
    /// <summary>The exact solver exceeded the configured size or time limit.</summary>
    ExceededLimit
}

/// <summary>A generated TSP instance with its reference optimal solution.</summary>
public class DatasetInstance
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int Index { get; set; }
    /// <summary>Number of points drawn for this instance; varies across the instances of a job.</summary>
    public int PointCount { get; set; }
    public int Seed { get; set; }
    public int GenerationAttempts { get; set; }
    public DatasetInstanceStatus Status { get; set; } = DatasetInstanceStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }

    /// <summary>JSON array of {lat,lng}.</summary>
    public string? CoordinatesJson { get; set; }
    /// <summary>JSON n×n cost matrix in meters — the exact matrix used by both solvers.</summary>
    public string? DistanceMatrixJson { get; set; }
    public int? FixedStartIndex { get; set; }

    public InstanceFeatures? Features { get; set; }

    public double? OptimalDistanceM { get; set; }
    /// <summary>JSON array of point indices.</summary>
    public string? OptimalTourJson { get; set; }
    public ExactMethod? ExactMethod { get; set; }
    public double? ExactTimeMs { get; set; }
    public long? ExactOperations { get; set; }

    public DatasetJob? Job { get; set; }
    public List<AcoRun> Runs { get; set; } = [];
}
