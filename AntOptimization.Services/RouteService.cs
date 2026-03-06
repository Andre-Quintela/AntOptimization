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
}
