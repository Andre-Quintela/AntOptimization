using AntOptimization.Domain.DTOs.Dataset;
using AntOptimization.Domain.Entities;

namespace AntOptimization.Services.Dataset;

/// <summary>Aggregations over ACO runs shared by the API and the CSV export.</summary>
public static class DatasetStatistics
{
    private const double TieTolerance = 1e-9;

    public record RunGroupStats(
        int RunCount,
        double MeanGap,
        double StdGap,
        double MinGap,
        double MaxGap,
        double OptimalHitRate,
        double MeanTimeMs,
        double MeanBestIteration);

    public static RunGroupStats Aggregate(IReadOnlyCollection<AcoRun> runs)
    {
        if (runs.Count == 0)
            return new RunGroupStats(0, 0, 0, 0, 0, 0, 0, 0);

        double mean = runs.Average(r => r.GapPercent);
        double std = runs.Count > 1
            ? Math.Sqrt(runs.Sum(r => (r.GapPercent - mean) * (r.GapPercent - mean)) / (runs.Count - 1))
            : 0;

        return new RunGroupStats(
            runs.Count,
            mean,
            std,
            runs.Min(r => r.GapPercent),
            runs.Max(r => r.GapPercent),
            runs.Count(r => r.ReachedOptimum) / (double)runs.Count,
            runs.Average(r => r.ExecutionTimeMs),
            runs.Average(r => r.BestIteration));
    }

    /// <summary>Per-configuration summary across all instances, ordered by mean gap then time.</summary>
    public static List<ConfigSummaryDto> SummarizeConfigurations(IEnumerable<AcoRun> allRuns)
    {
        var runs = allRuns.Where(r => r.Status == AcoRunStatus.Completed).ToList();

        // For each instance, the configurations with the lowest mean gap "win" it.
        var wins = runs
            .GroupBy(r => r.InstanceId)
            .SelectMany(instanceRuns =>
            {
                var means = instanceRuns
                    .GroupBy(r => r.ConfigKey)
                    .Select(g => (Key: g.Key, Mean: g.Average(r => r.GapPercent)))
                    .ToList();
                double best = means.Min(m => m.Mean);
                return means.Where(m => m.Mean <= best + TieTolerance).Select(m => m.Key);
            })
            .GroupBy(key => key)
            .ToDictionary(g => g.Key, g => g.Count());

        return runs
            .GroupBy(r => r.ConfigKey)
            .Select(g =>
            {
                var first = g.First();
                var stats = Aggregate(g.ToList());
                return new ConfigSummaryDto
                {
                    ConfigKey = g.Key,
                    Alpha = first.Alpha,
                    Beta = first.Beta,
                    NumberOfAnts = first.NumberOfAnts,
                    EvaporationRate = first.EvaporationRate,
                    Iterations = first.Iterations,
                    RunCount = stats.RunCount,
                    InstanceCount = g.Select(r => r.InstanceId).Distinct().Count(),
                    MeanGapPercent = stats.MeanGap,
                    StdGapPercent = stats.StdGap,
                    MinGapPercent = stats.MinGap,
                    MaxGapPercent = stats.MaxGap,
                    OptimalHitRate = stats.OptimalHitRate,
                    MeanExecutionTimeMs = stats.MeanTimeMs,
                    MeanBestIteration = stats.MeanBestIteration,
                    InstancesWon = wins.GetValueOrDefault(g.Key)
                };
            })
            .OrderBy(s => s.MeanGapPercent)
            .ThenBy(s => s.MeanExecutionTimeMs)
            .ToList();
    }
}
