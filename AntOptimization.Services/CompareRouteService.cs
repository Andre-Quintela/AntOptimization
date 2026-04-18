using System.Diagnostics;
using System.Text.Json;
using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Entities;
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
    private readonly ICompareRunRepository _repository;

    public CompareRouteService(
        IDistanceMatrixService distanceMatrixService,
        IAntColonyOptimizationService acoService,
        INearestNeighborService nnService,
        ITwoOptService twoOptService,
        IGeneticAlgorithmService gaService,
        ICompareRunRepository repository)
    {
        _distanceMatrixService = distanceMatrixService;
        _acoService = acoService;
        _nnService = nnService;
        _twoOptService = twoOptService;
        _gaService = gaService;
        _repository = repository;
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

        var minDistance = results.Min(r => r.TotalDistance);
        foreach (var result in results)
            result.RelativeGapPercent = minDistance > 0
                ? Math.Round((result.TotalDistance - minDistance) / minDistance * 100.0, 2)
                : 0.0;

        await _repository.AddAsync(new CompareRun
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            LocationCount = request.Locations.Count,
            StartLocationIndex = request.StartLocationIndex,
            Results = results.Select(r => new CompareRunResult
            {
                Id = Guid.NewGuid(),
                Algorithm = r.Algorithm,
                BestRouteOrderJson = JsonSerializer.Serialize(r.BestRouteOrder),
                TotalDistanceKm = r.TotalDistance,
                ExecutionTimeMs = r.ExecutionTimeMs,
                RelativeGapPercent = r.RelativeGapPercent,
                RouteCoordinatesJson = JsonSerializer.Serialize(r.RouteCoordinates)
            }).ToList()
        });

        return new CompareOptimizationResponse { Results = results };
    }
}
