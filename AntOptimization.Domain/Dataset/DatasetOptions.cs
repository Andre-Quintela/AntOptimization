using AntOptimization.Domain.Algorithms.Exact;

namespace AntOptimization.Domain.Dataset;

/// <summary>Limits and pacing for dataset generation (appsettings section "DatasetGeneration").</summary>
public class DatasetOptions
{
    public const string SectionName = "DatasetGeneration";

    public int BruteForceMaxPoints { get; set; } = 10;
    public int MaxExactPoints { get; set; } = 15;
    public int MaxInstancesPerJob { get; set; } = 200;
    public int MaxRepetitions { get; set; } = 30;
    public int MaxRunsPerJob { get; set; } = 200_000;
    public int ExactTimeoutSeconds { get; set; } = 60;
    /// <summary>Pause between OSRM requests to respect the public server's rate limits.</summary>
    public int OsrmDelayMs { get; set; } = 1000;
    /// <summary>How many times a point set is regenerated when OSRM cannot route between points.</summary>
    public int MaxGenerationAttempts { get; set; } = 3;
    /// <summary>Upper bound for the estimated wall-clock time of a job.</summary>
    public double MaxEstimatedHours { get; set; } = 12;

    public ExactSolverLimits ToExactLimits() => new()
    {
        BruteForceMaxPoints = BruteForceMaxPoints,
        MaxExactPoints = MaxExactPoints
    };
}

/// <summary>Fixed ACO implementation constants recorded in the dataset metadata.</summary>
public static class AcoConstants
{
    public const double InitialPheromone = 1.0;
    public const double DepositConstantQ = 1.0;
    public const string Objective = "open_path";
}
