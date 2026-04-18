using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;
using AntOptimization.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace AntOptimization.Tests.Services;

public class RouteServiceTests
{
    private static OptimizationRequest TwoLocationRequest => new()
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
        Mock<IOptimizationRunRepository>) CreateMocks(
        List<int>? tour = null,
        double distance = 1000.0,
        List<IterationSnapshot>? history = null)
    {
        tour ??= [0, 1];
        history ??= [];

        var mockDms = new Mock<IDistanceMatrixService>();
        var mockAco = new Mock<IAntColonyOptimizationService>();
        var mockRepo = new Mock<IOptimizationRunRepository>();

        mockDms.Setup(x => x.GetDistanceMatrixAsync(It.IsAny<List<Location>>()))
               .ReturnsAsync(new double[2, 2]);

        mockDms.Setup(x => x.GetRouteCoordinatesAsync(It.IsAny<List<Location>>()))
               .ReturnsAsync(new List<Location>());

        mockAco.Setup(x => x.Optimize(It.IsAny<double[,]>(), It.IsAny<int?>()))
               .Returns((tour, distance));

        mockAco.Setup(x => x.OptimizeWithHistory(It.IsAny<double[,]>(), It.IsAny<int?>()))
               .Returns((tour, distance, history));

        return (mockDms, mockAco, mockRepo);
    }

    [Fact]
    public async Task OptimizeRouteAsync_ConvertsMeteresToKilometres()
    {
        var (mockDms, mockAco, mockRepo) = CreateMocks(distance: 5000.0);
        var service = new RouteService(mockDms.Object, mockAco.Object, mockRepo.Object);

        var result = await service.OptimizeRouteAsync(TwoLocationRequest);

        result.TotalDistance.Should().Be(5.0);
    }

    [Fact]
    public async Task OptimizeRouteAsync_RoundsDistanceToTwoDecimals()
    {
        var (mockDms, mockAco, mockRepo) = CreateMocks(distance: 1234.567);
        var service = new RouteService(mockDms.Object, mockAco.Object, mockRepo.Object);

        var result = await service.OptimizeRouteAsync(TwoLocationRequest);

        result.TotalDistance.Should().Be(1.23);
    }

    [Fact]
    public async Task OptimizeRouteAsync_PassesLocationsToDistanceService()
    {
        var (mockDms, mockAco, mockRepo) = CreateMocks();
        List<Location>? captured = null;

        mockDms.Setup(x => x.GetDistanceMatrixAsync(It.IsAny<List<Location>>()))
               .Callback<List<Location>>(locs => captured = locs)
               .ReturnsAsync(new double[2, 2]);

        var service = new RouteService(mockDms.Object, mockAco.Object, mockRepo.Object);

        await service.OptimizeRouteAsync(TwoLocationRequest);

        captured.Should().NotBeNull();
        var locs = captured!;
        locs.Should().HaveCount(2);
        locs[0].Lat.Should().Be(10.0);
        locs[0].Lng.Should().Be(20.0);
        locs[1].Lat.Should().Be(30.0);
        locs[1].Lng.Should().Be(40.0);
    }

    [Fact]
    public async Task OptimizeRouteAsync_ReturnsCorrectBestRouteOrder()
    {
        var expectedOrder = new List<int> { 1, 0 };
        var (mockDms, mockAco, mockRepo) = CreateMocks(tour: expectedOrder);
        var service = new RouteService(mockDms.Object, mockAco.Object, mockRepo.Object);

        var result = await service.OptimizeRouteAsync(TwoLocationRequest);

        result.BestRouteOrder.Should().Equal(expectedOrder);
    }

    [Fact]
    public async Task OptimizeRouteAsync_PersistsOneRunPerCall()
    {
        var (mockDms, mockAco, mockRepo) = CreateMocks();
        var service = new RouteService(mockDms.Object, mockAco.Object, mockRepo.Object);

        await service.OptimizeRouteAsync(TwoLocationRequest);

        mockRepo.Verify(x => x.AddAsync(It.IsAny<OptimizationRun>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OptimizeRouteVisualAsync_YieldsHistoryCountPlusOneFinalEvent()
    {
        var history = new List<IterationSnapshot>
        {
            new(0, [0, 1], 1000, []),
            new(1, [0, 1],  900, [])
        };
        var (mockDms, mockAco, mockRepo) = CreateMocks(history: history);
        var service = new RouteService(mockDms.Object, mockAco.Object, mockRepo.Object);

        var events = new List<object>();
        await foreach (var evt in service.OptimizeRouteVisualAsync(TwoLocationRequest))
            events.Add(evt);

        // 2 IterationEvents + 1 final VisualOptimizationResult
        events.Should().HaveCount(3);
        events.Last().Should().BeOfType<VisualOptimizationResult>();
    }

    [Fact]
    public async Task OptimizeRouteVisualAsync_IterationEventsHaveCorrectData()
    {
        var history = new List<IterationSnapshot>
        {
            new(0, [0, 1], 2000, []),
            new(1, [0, 1], 1500, [])
        };
        var (mockDms, mockAco, mockRepo) = CreateMocks(history: history);
        var service = new RouteService(mockDms.Object, mockAco.Object, mockRepo.Object);

        var events = new List<object>();
        await foreach (var evt in service.OptimizeRouteVisualAsync(TwoLocationRequest))
            events.Add(evt);

        var firstEvent = events[0].Should().BeOfType<IterationEvent>().Subject;
        firstEvent.Iteration.Should().Be(0);
        firstEvent.TotalIterations.Should().Be(2);
        firstEvent.BestDistanceSoFar.Should().Be(2.0); // 2000m → 2.0km
    }

    [Fact]
    public async Task OptimizeRouteVisualAsync_ThrowsOnCancellation()
    {
        var history = new List<IterationSnapshot>
        {
            new(0, [0, 1], 1000, [])
        };
        var (mockDms, mockAco, mockRepo) = CreateMocks(history: history);
        var service = new RouteService(mockDms.Object, mockAco.Object, mockRepo.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancel so ThrowIfCancellationRequested fires on first iteration

        var act = async () =>
        {
            await foreach (var _ in service.OptimizeRouteVisualAsync(TwoLocationRequest, cts.Token)) { }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
