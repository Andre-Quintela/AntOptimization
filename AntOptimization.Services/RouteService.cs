using System.Runtime.CompilerServices;
using System.Text.Json;
using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;

namespace AntOptimization.Services;

public class RouteService : IRouteService
{
    private readonly IDistanceMatrixService _distanceMatrixService;
    private readonly IAntColonyOptimizationService _acoService;
    private readonly IOptimizationRunRepository _repository;

    public RouteService(
        IDistanceMatrixService distanceMatrixService,
        IAntColonyOptimizationService acoService,
        IOptimizationRunRepository repository)
    {
        _distanceMatrixService = distanceMatrixService;
        _acoService = acoService;
        _repository = repository;
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

        var response = new OptimizationResponse
        {
            BestRouteOrder = bestTour,
            TotalDistance = Math.Round(bestDistance / 1000, 2),
            RouteCoordinates = routeCoordinates
                .Select(l => new LocationDto { Lat = l.Lat, Lng = l.Lng })
                .ToList()
        };

        await _repository.AddAsync(new OptimizationRun
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            LocationCount = request.Locations.Count,
            StartLocationIndex = request.StartLocationIndex,
            BestRouteOrderJson = JsonSerializer.Serialize(response.BestRouteOrder),
            TotalDistanceKm = response.TotalDistance,
            RouteCoordinatesJson = JsonSerializer.Serialize(response.RouteCoordinates)
        });

        return response;
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
