using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;
using AntOptimization.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace AntOptimization.Tests.Services;

public class CompareRouteServiceTests
{
    private static CompareOptimizationRequest TwoLocationRequest => new()
    {
        Locations =
        [
            new LocationDto { Lat = 10.0, Lng = 20.0 },
            new LocationDto { Lat = 30.0, Lng = 40.0 }
        ]
    };

    private static (
        Mock<IDistanceMatrixService>,
        Mock<IAntColonyOptimizationService>,
        Mock<INearestNeighborService>,
        Mock<ITwoOptService>,
        Mock<IGeneticAlgorithmService>,
        Mock<ICompareRunRepository>) CreateMocks(double distance = 5000.0)
    {
        var mockDms = new Mock<IDistanceMatrixService>();
        var mockAco = new Mock<IAntColonyOptimizationService>();
        var mockNn = new Mock<INearestNeighborService>();
        var mockTwoOpt = new Mock<ITwoOptService>();
        var mockGa = new Mock<IGeneticAlgorithmService>();
        var mockRepo = new Mock<ICompareRunRepository>();

        mockDms.Setup(x => x.GetDistanceMatrixAsync(It.IsAny<List<Location>>()))
               .ReturnsAsync(new double[2, 2]);

        mockDms.Setup(x => x.GetRouteCoordinatesAsync(It.IsAny<List<Location>>()))
               .ReturnsAsync(new List<Location>());

        mockAco.Setup(x => x.Optimize(It.IsAny<double[,]>(), It.IsAny<int?>()))
               .Returns((new List<int> { 0, 1 }, distance));

        mockNn.Setup(x => x.Optimize(It.IsAny<double[,]>(), It.IsAny<int?>()))
              .Returns((new List<int> { 0, 1 }, distance + 1000));

        mockTwoOpt.Setup(x => x.Optimize(It.IsAny<double[,]>(), It.IsAny<int?>()))
                  .Returns((new List<int> { 0, 1 }, distance + 500));

        mockGa.Setup(x => x.Optimize(It.IsAny<double[,]>(), It.IsAny<int?>()))
              .Returns((new List<int> { 0, 1 }, distance + 200));

        return (mockDms, mockAco, mockNn, mockTwoOpt, mockGa, mockRepo);
    }

    private static CompareRouteService CreateService(
        Mock<IDistanceMatrixService> dms,
        Mock<IAntColonyOptimizationService> aco,
        Mock<INearestNeighborService> nn,
        Mock<ITwoOptService> twoOpt,
        Mock<IGeneticAlgorithmService> ga,
        Mock<ICompareRunRepository> repo)
        => new(dms.Object, aco.Object, nn.Object, twoOpt.Object, ga.Object, repo.Object);

    [Fact]
    public async Task CompareRoutesAsync_ReturnsResultForEachAlgorithm()
    {
        var (dms, aco, nn, twoOpt, ga, repo) = CreateMocks();
        var service = CreateService(dms, aco, nn, twoOpt, ga, repo);

        var response = await service.CompareRoutesAsync(TwoLocationRequest);

        response.Results.Should().HaveCount(4);
        response.Results.Select(r => r.Algorithm).Should()
            .BeEquivalentTo(["ACO", "Nearest Neighbor", "2-Opt", "Genetic Algorithm"]);
    }

    [Fact]
    public async Task CompareRoutesAsync_ExecutionTimeMsIsNonNegative()
    {
        var (dms, aco, nn, twoOpt, ga, repo) = CreateMocks();
        var service = CreateService(dms, aco, nn, twoOpt, ga, repo);

        var response = await service.CompareRoutesAsync(TwoLocationRequest);

        response.Results.Should().AllSatisfy(r => r.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0));
    }

    [Fact]
    public async Task CompareRoutesAsync_ConvertsMetersToKilometres()
    {
        var (dms, aco, nn, twoOpt, ga, repo) = CreateMocks(distance: 5000.0);
        var service = CreateService(dms, aco, nn, twoOpt, ga, repo);

        var response = await service.CompareRoutesAsync(TwoLocationRequest);

        var acoResult = response.Results.Single(r => r.Algorithm == "ACO");
        acoResult.TotalDistance.Should().Be(5.0);
    }

    [Fact]
    public async Task CompareRoutesAsync_CallsDistanceMatrixServiceOnce()
    {
        var (dms, aco, nn, twoOpt, ga, repo) = CreateMocks();
        var service = CreateService(dms, aco, nn, twoOpt, ga, repo);

        await service.CompareRoutesAsync(TwoLocationRequest);

        dms.Verify(x => x.GetDistanceMatrixAsync(It.IsAny<List<Location>>()), Times.Once);
    }

    [Fact]
    public async Task CompareRoutesAsync_BestAlgorithmHasZeroRelativeGap()
    {
        var (dms, aco, nn, twoOpt, ga, repo) = CreateMocks(distance: 5000.0);
        var service = CreateService(dms, aco, nn, twoOpt, ga, repo);

        var response = await service.CompareRoutesAsync(TwoLocationRequest);

        var best = response.Results.OrderBy(r => r.TotalDistance).First();
        best.RelativeGapPercent.Should().Be(0.0);
    }

    [Fact]
    public async Task CompareRoutesAsync_NonBestAlgorithmsHavePositiveRelativeGap()
    {
        var (dms, aco, nn, twoOpt, ga, repo) = CreateMocks(distance: 5000.0);
        var service = CreateService(dms, aco, nn, twoOpt, ga, repo);

        var response = await service.CompareRoutesAsync(TwoLocationRequest);

        var minDist = response.Results.Min(r => r.TotalDistance);
        response.Results
            .Where(r => r.TotalDistance > minDist)
            .Should().AllSatisfy(r => r.RelativeGapPercent.Should().BeGreaterThan(0.0));
    }

    [Fact]
    public async Task CompareRoutesAsync_PersistsOneRunPerCall()
    {
        var (dms, aco, nn, twoOpt, ga, repo) = CreateMocks();
        var service = CreateService(dms, aco, nn, twoOpt, ga, repo);

        await service.CompareRoutesAsync(TwoLocationRequest);

        repo.Verify(x => x.AddAsync(It.IsAny<CompareRun>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
