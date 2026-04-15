using AntOptimization.Services;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Services;

public class AntColonyOptimizationServiceTests
{
    private static double[,] ThreeCityMatrix => new double[,]
    {
        {  0, 50, 80 },
        { 50,  0, 60 },
        { 80, 60,  0 }
    };

    [Fact]
    public void Optimize_ReturnsValidTour_VisitsAllCities()
    {
        var service = new AntColonyOptimizationService();

        var (bestTour, _) = service.Optimize(ThreeCityMatrix);

        bestTour.Should().HaveCount(3);
        bestTour.Should().OnlyHaveUniqueItems();
        bestTour.Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public void Optimize_WithFixedStart_TourBeginsAtCorrectCity()
    {
        var service = new AntColonyOptimizationService();

        var (bestTour, _) = service.Optimize(ThreeCityMatrix, startCity: 1);

        bestTour[0].Should().Be(1);
    }

    [Fact]
    public void Optimize_ReturnsPositiveDistance()
    {
        var service = new AntColonyOptimizationService();

        var (_, bestDistance) = service.Optimize(ThreeCityMatrix);

        bestDistance.Should().BePositive();
    }

    [Fact]
    public void OptimizeWithHistory_ReturnsDefaultIterationsCount()
    {
        var service = new AntColonyOptimizationService();

        var (_, _, history) = service.OptimizeWithHistory(ThreeCityMatrix);

        // DefaultParameters uses 100 iterations.
        history.Should().HaveCount(100);
    }

    [Fact]
    public void OptimizeWithHistory_ReturnsValidBestTour()
    {
        var service = new AntColonyOptimizationService();

        var (bestTour, _, _) = service.OptimizeWithHistory(ThreeCityMatrix);

        bestTour.Should().HaveCount(3);
        bestTour.Should().OnlyHaveUniqueItems();
    }
}
