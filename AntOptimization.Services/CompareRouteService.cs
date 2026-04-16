using System.Diagnostics;
using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;

namespace AntOptimization.Services;

public class CompareRouteService : ICompareRouteService
{
    private readonly IDistanceMatrixService _distanceMatrixService;
    private readonly IAntColonyOptimizationService _acoService;
    private readonly INearestNeighborService _nnService;
    private readonly ITwoOptService _twoOptService;
    private readonly IGeneticAlgorithmService _gaService;

    public CompareRouteService(
        IDistanceMatrixService distanceMatrixService,
        IAntColonyOptimizationService acoService,
        INearestNeighborService nnService,
        ITwoOptService twoOptService,
        IGeneticAlgorithmService gaService)
    {
        _distanceMatrixService = distanceMatrixService;
        _acoService = acoService;
        _nnService = nnService;
        _twoOptService = twoOptService;
        _gaService = gaService;
    }

    public async Task<CompareOptimizationResponse> CompareRoutesAsync(CompareOptimizationRequest request)
    {
        var locations = request.Locations
            .Select(l => new Location { Lat = l.Lat, Lng = l.Lng })
            .ToList();

        var distanceMatrix = await _distanceMatrixService.GetDistanceMatrixAsync(locations);

        var algorithms = new List<(string Name, Func<(List<int> Tour, double Distance)> Optimize)>
        {
            ("ACO", () => _acoService.Optimize(distanceMatrix, request.StartLocationIndex)),
            ("Nearest Neighbor", () => _nnService.Optimize(distanceMatrix, request.StartLocationIndex)),
            ("2-Opt", () => _twoOptService.Optimize(distanceMatrix, request.StartLocationIndex)),
            ("Genetic Algorithm", () => _gaService.Optimize(distanceMatrix, request.StartLocationIndex))
        };

        var results = new List<AlgorithmResult>();

        foreach (var (name, optimize) in algorithms)
        {
            var sw = Stopwatch.StartNew();
            var (bestTour, bestDistance) = optimize();
            sw.Stop();

            var orderedLocations = bestTour.Select(i => locations[i]).ToList();
            var routeCoordinates = await _distanceMatrixService.GetRouteCoordinatesAsync(orderedLocations);

            results.Add(new AlgorithmResult
            {
                Algorithm = name,
                BestRouteOrder = bestTour,
                TotalDistance = Math.Round(bestDistance / 1000, 2),
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                RouteCoordinates = routeCoordinates
                    .Select(l => new LocationDto { Lat = l.Lat, Lng = l.Lng })
                    .ToList()
            });
        }

        return new CompareOptimizationResponse { Results = results };
    }
}
