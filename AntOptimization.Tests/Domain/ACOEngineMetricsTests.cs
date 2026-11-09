using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Algorithms.Exact;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class ACOEngineMetricsTests
{
    private static ACOParameters Parameters => new()
    {
        NumberOfAnts = 6,
        EvaporationRate = 0.3,
        Alpha = 1.0,
        Beta = 2.0,
        Iterations = 25
    };

    private static double[,] Matrix()
    {
        var random = new Random(7);
        var matrix = new double[8, 8];
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                if (i != j) matrix[i, j] = 1 + random.NextDouble() * 100;
        return matrix;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalResults()
    {
        var a = new ACOEngine(Parameters, seed: 123).SolveWithMetrics(Matrix());
        var b = new ACOEngine(Parameters, seed: 123).SolveWithMetrics(Matrix());

        a.BestTour.Should().Equal(b.BestTour);
        a.BestDistance.Should().Be(b.BestDistance);
        a.ConvergenceCurve.Should().Equal(b.ConvergenceCurve);
    }

    [Fact]
    public void SolveWithMetrics_ReturnsCoherentMetrics()
    {
        var matrix = Matrix();
        var metrics = new ACOEngine(Parameters, seed: 5).SolveWithMetrics(matrix, 0);
        var optimum = new BruteForceEngine().Solve(matrix, 0).Distance;

        metrics.ConvergenceCurve.Should().HaveCount(Parameters.Iterations);
        metrics.ConvergenceCurve.Should().BeInDescendingOrder();
        metrics.ConvergenceCurve[^1].Should().Be(metrics.BestDistance);
        metrics.BestIteration.Should().BeInRange(0, Parameters.Iterations - 1);
        metrics.ConvergenceCurve[metrics.BestIteration].Should().Be(metrics.BestDistance);
        metrics.ToursConstructed.Should().Be(Parameters.NumberOfAnts * Parameters.Iterations);
        metrics.BestTour[0].Should().Be(0);
        PathMath.PathDistance(metrics.BestTour, matrix).Should().BeApproximately(metrics.BestDistance, 1e-9);
        metrics.BestDistance.Should().BeGreaterThanOrEqualTo(optimum - 1e-9);
    }

    [Fact]
    public void SolveWithMetrics_MatchesSolve_ForSameSeed()
    {
        var (tour, distance) = new ACOEngine(Parameters, seed: 9).Solve(Matrix());
        var metrics = new ACOEngine(Parameters, seed: 9).SolveWithMetrics(Matrix());

        metrics.BestTour.Should().Equal(tour);
        metrics.BestDistance.Should().Be(distance);
    }
}
