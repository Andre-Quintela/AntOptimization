namespace AntOptimization.Domain.Algorithms.Exact;

public enum ExactMethod
{
    BruteForce,
    HeldKarp
}

/// <param name="Operations">Search nodes (brute force) or DP transitions (Held-Karp) evaluated.</param>
public record ExactSolution(
    List<int> Tour,
    double Distance,
    ExactMethod Method,
    long Operations
);
