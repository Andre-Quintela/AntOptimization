using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services;

public class TwoOptService : ITwoOptService
{
    public (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null)
    {
        var engine = new TwoOptEngine();
        return engine.Solve(distanceMatrix, startCity);
    }
}
