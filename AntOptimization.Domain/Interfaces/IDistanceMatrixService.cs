using AntOptimization.Domain.Models;

namespace AntOptimization.Domain.Interfaces;

public interface IDistanceMatrixService
{
    Task<double[,]> GetDistanceMatrixAsync(List<Location> locations);
    Task<List<Location>> GetRouteCoordinatesAsync(List<Location> orderedLocations);
}
