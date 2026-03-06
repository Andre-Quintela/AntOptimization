namespace AntOptimization.Domain.Algorithms;

public record IterationSnapshot(
    int Iteration,
    List<int> GlobalBestTour,
    double GlobalBestDistance,
    List<List<int>> AntTours
);
