using System.Runtime.CompilerServices;
using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;

namespace AntOptimization.Services;

public class RouteService : IRouteService
{
    private readonly IDistanceMatrixService _distanceMatrixService;
    private readonly IAntColonyOptimizationService _acoService;

    public RouteService(IDistanceMatrixService distanceMatrixService, IAntColonyOptimizationService acoService)
    {
        _distanceMatrixService = distanceMatrixService;
        _acoService = acoService;
    }

    public async Task<OptimizationResponse> OptimizeRouteAsync(OptimizationRequest request)
    {
        var locations = request.Locations
            .Select(l => new Location { Lat = l.Lat, Lng = l.Lng })
            .ToList();

        var distanceMatrix = await _distanceMatrixService.GetDistanceMatrixAsync(locations);

        var (bestTour, bestDistance) = _acoService.Optimize(distanceMatrix, request.StartLocationIndex);

        var orderedLocations = bestTour.Select(i => locations[i]).ToList();

        var routeCoordinates = await _distanceMatrixService.GetRouteCoordinatesAsync(orderedLocations);

        return new OptimizationResponse
        {
            BestRouteOrder = bestTour,
            TotalDistance = Math.Round(bestDistance / 1000, 2),
            RouteCoordinates = routeCoordinates
                .Select(l => new LocationDto { Lat = l.Lat, Lng = l.Lng })
                .ToList()
        };
    }

    public async IAsyncEnumerable<object> OptimizeRouteVisualAsync(
        OptimizationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var locations = request.Locations
            .Select(l => new Location { Lat = l.Lat, Lng = l.Lng })
            .ToList();

        var distanceMatrix = await _distanceMatrixService.GetDistanceMatrixAsync(locations);

        var (bestTour, bestDistance, history) = _acoService.OptimizeWithHistory(distanceMatrix, request.StartLocationIndex);

        foreach (var snapshot in history)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return new IterationEvent
            {
                Iteration = snapshot.Iteration,
                TotalIterations = history.Count,
                BestTourSoFar = snapshot.GlobalBestTour,
                BestDistanceSoFar = Math.Round(snapshot.GlobalBestDistance / 1000, 2),
                AntTours = snapshot.AntTours
            };

            await Task.Delay(50, cancellationToken);
        }

        var orderedLocations = bestTour.Select(i => locations[i]).ToList();
        var routeCoordinates = await _distanceMatrixService.GetRouteCoordinatesAsync(orderedLocations);

        yield return new VisualOptimizationResult
        {
            BestRouteOrder = bestTour,
            TotalDistance = Math.Round(bestDistance / 1000, 2),
            RouteCoordinates = routeCoordinates
                .Select(l => new LocationDto { Lat = l.Lat, Lng = l.Lng })
                .ToList()
        };
    }
}
