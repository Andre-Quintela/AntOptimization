namespace AntOptimization.Domain.Algorithms;

/// <summary>
/// Result of a single ACO run with the metrics needed for experimental analysis.
/// </summary>
/// <param name="BestIteration">Zero-based iteration in which the best tour was first found.</param>
/// <param name="ToursConstructed">Number of complete tours built (ants × iterations).</param>
/// <param name="ConvergenceCurve">Best distance so far at the end of each iteration.</param>
public record ACORunMetrics(
    List<int> BestTour,
    double BestDistance,
    int BestIteration,
    long ToursConstructed,
    double[] ConvergenceCurve
);
