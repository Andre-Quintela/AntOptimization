using AntOptimization.Domain.DTOs.Dataset;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services.Dataset;

internal static class DatasetMapper
{
    public static DatasetJobDto ToDto(DatasetJob job)
    {
        int runsPerInstance = job.ConfigurationCount * job.Repetitions;
        // Failed instances never produce runs; count them as done so progress reaches 100%.
        double done = job.CompletedRuns + (double)job.FailedInstances * runsPerInstance;

        return new DatasetJobDto
        {
            Id = job.Id,
            Name = job.Name,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt,
            Status = job.Status,
            ErrorMessage = job.ErrorMessage,
            Region = new RegionDto { South = job.South, West = job.West, North = job.North, East = job.East },
            PointCounts = DatasetJson.DeserializePointCounts(job.PointCountsJson),
            InstancesPerPointCount = job.InstancesPerPointCount,
            InstanceCount = job.InstanceCount,
            DistanceSource = job.DistanceSource,
            FixedStart = job.FixedStart,
            Repetitions = job.Repetitions,
            MasterSeed = job.MasterSeed,
            Grid = DatasetJson.DeserializeGrid(job.GridJson),
            ConfigurationCount = job.ConfigurationCount,
            TotalRuns = job.TotalRuns,
            CompletedRuns = job.CompletedRuns,
            ProcessedInstances = job.ProcessedInstances,
            FailedInstances = job.FailedInstances,
            ProgressPercent = job.TotalRuns > 0 ? Math.Round(Math.Min(100, done / job.TotalRuns * 100), 1) : 0
        };
    }

    public static DatasetInstanceDto ToDto(DatasetInstance instance, InstanceRunStats? stats) => new()
    {
        Id = instance.Id,
        Index = instance.Index,
        PointCount = instance.PointCount,
        Seed = instance.Seed,
        Status = instance.Status,
        ErrorMessage = instance.ErrorMessage,
        GenerationAttempts = instance.GenerationAttempts,
        Coordinates = DatasetJson.DeserializeLocations(instance.CoordinatesJson),
        FixedStartIndex = instance.FixedStartIndex,
        Features = instance.Features,
        OptimalDistanceM = instance.OptimalDistanceM,
        OptimalTour = DatasetJson.DeserializeTour(instance.OptimalTourJson),
        ExactMethod = instance.ExactMethod?.ToString(),
        ExactTimeMs = instance.ExactTimeMs,
        ExactOperations = instance.ExactOperations,
        RunCount = stats?.RunCount ?? 0,
        BestGapPercent = stats?.BestGapPercent,
        MeanGapPercent = stats?.MeanGapPercent
    };

    public static AcoRunDto ToDto(AcoRun run) => new()
    {
        Id = run.Id,
        ConfigKey = run.ConfigKey,
        Repetition = run.Repetition,
        Seed = run.Seed,
        Status = run.Status,
        ErrorMessage = run.ErrorMessage,
        Alpha = run.Alpha,
        Beta = run.Beta,
        NumberOfAnts = run.NumberOfAnts,
        EvaporationRate = run.EvaporationRate,
        Iterations = run.Iterations,
        BestDistanceM = run.BestDistanceM,
        GapAbsM = run.GapAbsM,
        GapPercent = run.GapPercent,
        ReachedOptimum = run.ReachedOptimum,
        BestIteration = run.BestIteration,
        FirstOptimalIteration = run.FirstOptimalIteration,
        ToursConstructed = run.ToursConstructed,
        ExecutionTimeMs = run.ExecutionTimeMs,
        BestTour = DatasetJson.DeserializeTour(run.BestTourJson)
    };
}
