namespace AntOptimization.Domain.Algorithms;

/// <summary>
/// Shared helpers for the open-path TSP objective used by all engines
/// (the route does not return to the starting city).
/// </summary>
public static class PathMath
{
    public static double PathDistance(IReadOnlyList<int> tour, double[,] distances)
    {
        double total = 0;
        for (int i = 0; i < tour.Count - 1; i++)
            total += distances[tour[i], tour[i + 1]];
        return total;
    }
}
