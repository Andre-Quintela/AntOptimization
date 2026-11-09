using AntOptimization.Domain.Algorithms;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class TwoOptEngineTests
{
    private static double[,] FourCityMatrix => new double[,]
    {
        {   0, 100, 200, 150 },
        { 100,   0, 120, 180 },
        { 200, 120,   0,  90 },
        { 150, 180,  90,   0 }
    };

    [Fact]
    public void Solve_ReturnsValidTour_VisitsAllCities()
    {
        var engine = new TwoOptEngine();

        var (bestTour, _) = engine.Solve(FourCityMatrix);

        bestTour.Should().HaveCount(4);
        bestTour.Should().OnlyHaveUniqueItems();
        bestTour.Should().BeEquivalentTo(new[] { 0, 1, 2, 3 });
    }

    [Fact]
    public void Solve_WithFixedStartCity_TourBeginsAtCorrectCity()
    {
        var engine = new TwoOptEngine();

        var (bestTour, _) = engine.Solve(FourCityMatrix, fixedStartCity: 2);

        bestTour[0].Should().Be(2);
    }

    [Fact]
    public void Solve_ReturnsPositiveBestDistance()
    {
        var engine = new TwoOptEngine();

        var (_, bestDistance) = engine.Solve(FourCityMatrix);

        bestDistance.Should().BePositive();
    }

    [Fact]
    public void Solve_TwoCity_ReturnsCorrectDistance()
    {
        var matrix = new double[,]
        {
            {   0, 100 },
            { 100,   0 }
        };
        var engine = new TwoOptEngine();

        var (bestTour, bestDistance) = engine.Solve(matrix, fixedStartCity: 0);

        bestTour.Should().Equal(new[] { 0, 1 });
        bestDistance.Should().Be(100);
    }

    [Fact]
    public void Solve_DoesNotWorsenNearestNeighborSolution()
    {
        var nn = new NearestNeighborEngine();
        var (_, nnDistance) = nn.Solve(FourCityMatrix);

        var twoOpt = new TwoOptEngine();
        var (_, twoOptDistance) = twoOpt.Solve(FourCityMatrix);

        twoOptDistance.Should().BeLessThanOrEqualTo(nnDistance + 1e-6);
    }

    /// <summary>
    /// Asymmetric cost matrix (OSRM road distances): reversing a segment also flips its internal
    /// edges, so a delta built only from the boundary edges reports a distance that belongs to no
    /// actual route - and can make a move and its inverse look improving forever.
    /// </summary>
    [Fact]
    public void Solve_OnAsymmetricMatrices_ReportsTheCostOfTheReturnedTour()
    {
        var random = new Random(20261109);
        var engine = new TwoOptEngine();
        var nn = new NearestNeighborEngine();

        for (int trial = 0; trial < 200; trial++)
        {
            int n = random.Next(4, 15);
            var matrix = AsymmetricMatrix(n, random);

            var (tour, distance) = engine.Solve(matrix, fixedStartCity: 0);

            tour.Should().HaveCount(n).And.OnlyHaveUniqueItems();
            tour[0].Should().Be(0);
            distance.Should().BeApproximately(PathMath.PathDistance(tour, matrix), 1e-9);

            var (_, nnDistance) = nn.Solve(matrix, fixedStartCity: 0);
            distance.Should().BeLessThanOrEqualTo(nnDistance + 1e-6);
        }
    }

    [Fact]
    public void Solve_OnAsymmetricMatrix_AccountsForTheReversedInnerEdges()
    {
        // Going "up" the indices is cheap, coming back down is expensive: any reversal of an
        // inner segment is costly, so a boundary-only delta would misjudge the move.
        int n = 6;
        var matrix = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                matrix[i, j] = i == j ? 0 : (j > i ? 10 : 500);

        var (tour, distance) = new TwoOptEngine().Solve(matrix, fixedStartCity: 0);

        distance.Should().BeApproximately(PathMath.PathDistance(tour, matrix), 1e-9);
        tour.Should().Equal(new[] { 0, 1, 2, 3, 4, 5 });
        distance.Should().Be(50);
    }

    private static double[,] AsymmetricMatrix(int n, Random random)
    {
        var matrix = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                matrix[i, j] = i == j ? 0 : Math.Round(random.NextDouble() * 10_000, 3);
        return matrix;
    }
}
