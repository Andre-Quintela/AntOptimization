namespace AntOptimization.Domain.Algorithms.Exact;

/// <summary>
/// Exact open-path TSP solver by exhaustive enumeration of permutations,
/// with branch-and-bound pruning on the partial path cost.
/// Complexity O(n!) — only viable for very small instances.
/// </summary>
public class BruteForceEngine
{
    public ExactSolution Solve(double[,] distanceMatrix, int? fixedStartCity = null, CancellationToken cancellationToken = default)
    {
        int n = distanceMatrix.GetLength(0);
        var state = new SearchState(n);

        IEnumerable<int> starts = fixedStartCity.HasValue ? [fixedStartCity.Value] : Enumerable.Range(0, n);

        foreach (int start in starts)
        {
            state.Path[0] = start;
            state.Visited[start] = true;
            Search(distanceMatrix, state, depth: 1, cost: 0, cancellationToken);
            state.Visited[start] = false;
        }

        return new ExactSolution(state.BestPath.ToList(), state.BestCost, ExactMethod.BruteForce, state.NodesEvaluated);
    }

    private static void Search(double[,] d, SearchState s, int depth, double cost, CancellationToken ct)
    {
        s.NodesEvaluated++;
        if ((s.NodesEvaluated & 0xFFFF) == 0)
            ct.ThrowIfCancellationRequested();

        if (depth == s.N)
        {
            if (cost < s.BestCost)
            {
                s.BestCost = cost;
                Array.Copy(s.Path, s.BestPath, s.N);
            }
            return;
        }

        int last = s.Path[depth - 1];
        for (int next = 0; next < s.N; next++)
        {
            if (s.Visited[next]) continue;

            double newCost = cost + d[last, next];
            if (newCost >= s.BestCost) continue;

            s.Visited[next] = true;
            s.Path[depth] = next;
            Search(d, s, depth + 1, newCost, ct);
            s.Visited[next] = false;
        }
    }

    private sealed class SearchState(int n)
    {
        public int N { get; } = n;
        public int[] Path { get; } = new int[n];
        public int[] BestPath { get; } = new int[n];
        public bool[] Visited { get; } = new bool[n];
        public double BestCost { get; set; } = double.MaxValue;
        public long NodesEvaluated { get; set; }
    }
}
