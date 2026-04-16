using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services;

public class NearestNeighborService : INearestNeighborService
{
    public (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null)
    {
        var engine = new NearestNeighborEngine();
        return engine.Solve(distanceMatrix, startCity);
    }
}
