using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;

namespace AntOptimization.Services.Dataset;

public class DatasetCostMatrixProvider : IDatasetCostMatrixProvider
{
    private readonly IDistanceMatrixService _distanceMatrixService;

    public DatasetCostMatrixProvider(IDistanceMatrixService distanceMatrixService)
    {
        _distanceMatrixService = distanceMatrixService;
    }

    public Task<double[,]> GetMatrixAsync(List<Location> locations, DistanceSource source, CancellationToken cancellationToken = default) =>
        source switch
        {
            DistanceSource.Osrm => _distanceMatrixService.GetDistanceMatrixAsync(locations),
            DistanceSource.Haversine => Task.FromResult(HaversineDistance.Matrix(locations)),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
}
