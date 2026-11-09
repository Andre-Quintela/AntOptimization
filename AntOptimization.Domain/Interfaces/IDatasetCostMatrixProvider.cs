using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Models;

namespace AntOptimization.Domain.Interfaces;

/// <summary>Builds the cost matrix (meters) used by both the exact solver and the ACO.</summary>
public interface IDatasetCostMatrixProvider
{
    Task<double[,]> GetMatrixAsync(List<Location> locations, DistanceSource source, CancellationToken cancellationToken = default);
}
