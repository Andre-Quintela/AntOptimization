namespace AntOptimization.Domain.Algorithms.Exact;

public class ExactSolverLimits
{
    /// <summary>Instances up to this size are solved by brute force.</summary>
    public int BruteForceMaxPoints { get; set; } = 10;

    /// <summary>Instances above this size are rejected (Held-Karp memory/time grows as n²·2ⁿ).</summary>
    public int MaxExactPoints { get; set; } = 15;
}

public record ExactCostEstimate(
    int Points,
    ExactMethod? Method,
    double EstimatedOperations,
    double EstimatedMemoryBytes,
    bool IsFeasible,
    string? Reason
);

/// <summary>
/// Chooses the exact algorithm for an instance and estimates its cost before running it.
/// </summary>
public class ExactSolver
{
    /// <summary>Hard cap independent of configuration: sbyte parents and the 2ⁿ table size.</summary>
    public const int AbsoluteMaxPoints = 20;

    private readonly ExactSolverLimits _limits;

    public ExactSolver(ExactSolverLimits limits)
    {
        _limits = limits;
    }

    public ExactCostEstimate Estimate(int points, bool fixedStart)
    {
        if (points < 2)
            return new ExactCostEstimate(points, null, 0, 0, false, "São necessários pelo menos 2 pontos.");

        int maxPoints = Math.Min(_limits.MaxExactPoints, AbsoluteMaxPoints);
        if (points > maxPoints)
            return new ExactCostEstimate(points, null, double.PositiveInfinity, double.PositiveInfinity, false,
                $"O cálculo exato está limitado a {maxPoints} pontos (crescimento exponencial/fatorial).");

        if (points <= _limits.BruteForceMaxPoints)
        {
            // Worst case without pruning: (n-1)! paths per start, n starts when the start is free.
            double operations = Factorial(points - 1) * (fixedStart ? 1 : points);
            return new ExactCostEstimate(points, ExactMethod.BruteForce, operations, points * sizeof(int) * 3, true, null);
        }

        double states = Math.Pow(2, points) * points;
        return new ExactCostEstimate(points, ExactMethod.HeldKarp, states * points, states * (sizeof(double) + sizeof(sbyte)), true, null);
    }

    public ExactSolution Solve(double[,] distanceMatrix, int? fixedStartCity = null, CancellationToken cancellationToken = default)
    {
        int n = distanceMatrix.GetLength(0);
        var estimate = Estimate(n, fixedStartCity.HasValue);

        if (!estimate.IsFeasible)
            throw new ExactLimitExceededException(estimate.Reason!);

        return estimate.Method == ExactMethod.BruteForce
            ? new BruteForceEngine().Solve(distanceMatrix, fixedStartCity, cancellationToken)
            : new HeldKarpEngine().Solve(distanceMatrix, fixedStartCity, cancellationToken);
    }

    private static double Factorial(int n)
    {
        double result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return result;
    }
}

public class ExactLimitExceededException(string message) : Exception(message);
