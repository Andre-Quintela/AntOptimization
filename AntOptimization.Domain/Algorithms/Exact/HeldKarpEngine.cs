namespace AntOptimization.Domain.Algorithms.Exact;

/// <summary>
/// Exact open-path TSP solver using the Held-Karp dynamic programming algorithm.
/// dp[mask, j] = minimum cost of a path that visits exactly the cities in <c>mask</c> and ends at <c>j</c>.
/// Complexity O(n²·2ⁿ) time and O(n·2ⁿ) memory. Supports asymmetric matrices.
/// </summary>
public class HeldKarpEngine
{
    public ExactSolution Solve(double[,] distanceMatrix, int? fixedStartCity = null, CancellationToken cancellationToken = default)
    {
        int n = distanceMatrix.GetLength(0);

        if (n == 1)
            return new ExactSolution([0], 0, ExactMethod.HeldKarp, 1);

        int full = 1 << n;
        var dp = new double[full * n];
        var parent = new sbyte[full * n];
        Array.Fill(dp, double.PositiveInfinity);
        Array.Fill(parent, (sbyte)-1);

        if (fixedStartCity.HasValue)
            dp[(1 << fixedStartCity.Value) * n + fixedStartCity.Value] = 0;
        else
            for (int j = 0; j < n; j++)
                dp[(1 << j) * n + j] = 0;

        long operations = 0;

        for (int mask = 1; mask < full; mask++)
        {
            if ((mask & 0x3FF) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            for (int last = 0; last < n; last++)
            {
                if ((mask & (1 << last)) == 0) continue;

                double current = dp[mask * n + last];
                if (double.IsPositiveInfinity(current)) continue;

                for (int next = 0; next < n; next++)
                {
                    if ((mask & (1 << next)) != 0) continue;

                    operations++;
                    int nextMask = mask | (1 << next);
                    double candidate = current + distanceMatrix[last, next];
                    int index = nextMask * n + next;

                    if (candidate < dp[index])
                    {
                        dp[index] = candidate;
                        parent[index] = (sbyte)last;
                    }
                }
            }
        }

        int fullMask = full - 1;
        int bestEnd = -1;
        double bestCost = double.PositiveInfinity;
        for (int j = 0; j < n; j++)
        {
            double cost = dp[fullMask * n + j];
            if (cost < bestCost)
            {
                bestCost = cost;
                bestEnd = j;
            }
        }

        var tour = new List<int>(n);
        int m = fullMask, city = bestEnd;
        while (city >= 0)
        {
            tour.Add(city);
            int previous = parent[m * n + city];
            m &= ~(1 << city);
            city = previous;
        }
        tour.Reverse();

        return new ExactSolution(tour, bestCost, ExactMethod.HeldKarp, operations);
    }
}
