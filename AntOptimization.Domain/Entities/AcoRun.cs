namespace AntOptimization.Domain.Entities;

public enum AcoRunStatus
{
    Completed,
    Failed
}

/// <summary>One ACO execution of a hyperparameter configuration on an instance.</summary>
public class AcoRun
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public int Repetition { get; set; }
    public int Seed { get; set; }
    public AcoRunStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    // Hyperparameters
    public double Alpha { get; set; }
    public double Beta { get; set; }
    public int NumberOfAnts { get; set; }
    public double EvaporationRate { get; set; }
    public int Iterations { get; set; }

    // Results (meters / milliseconds)
    public double BestDistanceM { get; set; }
    public double GapAbsM { get; set; }
    public double GapPercent { get; set; }
    public bool ReachedOptimum { get; set; }
    /// <summary>Zero-based iteration in which the run's best tour was found.</summary>
    public int BestIteration { get; set; }
    /// <summary>First iteration whose best-so-far equals the optimum, or null if never reached.</summary>
    public int? FirstOptimalIteration { get; set; }
    public long ToursConstructed { get; set; }
    public double ExecutionTimeMs { get; set; }
    /// <summary>JSON array of point indices.</summary>
    public string BestTourJson { get; set; } = "[]";

    public DatasetInstance? Instance { get; set; }
}
