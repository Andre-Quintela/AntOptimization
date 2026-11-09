using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Algorithms.Exact;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class ExactSolverTests
{
    private static double[,] RandomMatrix(int n, int seed, bool symmetric)
    {
        var random = new Random(seed);
        var matrix = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;
                if (symmetric && j < i) { matrix[i, j] = matrix[j, i]; continue; }
                matrix[i, j] = Math.Round(10 + random.NextDouble() * 990, 3);
            }
        return matrix;
    }

    private static IEnumerable<IEnumerable<int>> Permutations(IReadOnlyList<int> items)
    {
        if (items.Count <= 1) { yield return items; yield break; }
        for (int i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, idx) => idx != i).ToList();
            foreach (var perm in Permutations(rest))
                yield return new[] { items[i] }.Concat(perm);
        }
    }

    /// <summary>Plain enumeration without pruning — the oracle for the exact engines.</summary>
    private static double NaiveOptimum(double[,] matrix, int? fixedStart)
    {
        int n = matrix.GetLength(0);
        return Permutations(Enumerable.Range(0, n).ToList())
            .Select(p => p.ToList())
            .Where(p => fixedStart is null || p[0] == fixedStart)
            .Min(p => PathMath.PathDistance(p, matrix));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void BruteForce_MatchesNaiveEnumeration(int n)
    {
        for (int seed = 0; seed < 5; seed++)
        {
            var matrix = RandomMatrix(n, seed, symmetric: seed % 2 == 0);

            new BruteForceEngine().Solve(matrix).Distance.Should().BeApproximately(NaiveOptimum(matrix, null), 1e-6);
            new BruteForceEngine().Solve(matrix, 0).Distance.Should().BeApproximately(NaiveOptimum(matrix, 0), 1e-6);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    public void HeldKarp_MatchesBruteForce_OnSymmetricAndAsymmetricMatrices(int n)
    {
        for (int seed = 0; seed < 6; seed++)
        {
            var matrix = RandomMatrix(n, seed * 31 + n, symmetric: seed % 2 == 0);
            int? start = seed % 3 == 0 ? null : seed % n;

            var bf = new BruteForceEngine().Solve(matrix, start);
            var hk = new HeldKarpEngine().Solve(matrix, start);

            hk.Distance.Should().BeApproximately(bf.Distance, 1e-6);
        }
    }

    [Fact]
    public void HeldKarp_ReturnsConsistentTour()
    {
        var matrix = RandomMatrix(10, 42, symmetric: false);

        var result = new HeldKarpEngine().Solve(matrix, 3);

        result.Tour.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        result.Tour[0].Should().Be(3);
        PathMath.PathDistance(result.Tour, matrix).Should().BeApproximately(result.Distance, 1e-6);
    }

    [Fact]
    public void ExactSolver_KnownInstance_FindsOpenPathOptimum()
    {
        // Points on a line: 0 — 1 — 2 — 3. Open-path optimum from 0 is 0→1→2→3 = 3.
        var matrix = new double[,]
        {
            { 0, 1, 2, 3 },
            { 1, 0, 1, 2 },
            { 2, 1, 0, 1 },
            { 3, 2, 1, 0 }
        };

        var result = new ExactSolver(new ExactSolverLimits()).Solve(matrix, 0);

        result.Distance.Should().Be(3);
        result.Tour.Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void ExactSolver_ChoosesMethodBySize()
    {
        var solver = new ExactSolver(new ExactSolverLimits { BruteForceMaxPoints = 6, MaxExactPoints = 12 });

        solver.Estimate(6, true).Method.Should().Be(ExactMethod.BruteForce);
        solver.Estimate(7, true).Method.Should().Be(ExactMethod.HeldKarp);
        solver.Solve(RandomMatrix(8, 1, true), 0).Method.Should().Be(ExactMethod.HeldKarp);
    }

    [Fact]
    public void ExactSolver_AboveLimit_IsRejected()
    {
        var solver = new ExactSolver(new ExactSolverLimits { MaxExactPoints = 8 });

        solver.Estimate(9, true).IsFeasible.Should().BeFalse();
        var act = () => solver.Solve(RandomMatrix(9, 1, true));
        act.Should().Throw<ExactLimitExceededException>();
    }

    [Fact]
    public void ExactSolver_HonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => new HeldKarpEngine().Solve(RandomMatrix(14, 1, true), null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }
}
