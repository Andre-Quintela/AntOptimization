using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services;

public class AntColonyOptimizationService : IAntColonyOptimizationService
{
    public (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null)
    {
        var parameters = new ACOParameters
        {
            NumberOfAnts = 20,
            EvaporationRate = 0.5,
            Alpha = 1.0,
            Beta = 2.0,
            Iterations = 100
        };

        var engine = new ACOEngine(parameters);
        return engine.Solve(distanceMatrix, startCity);
    }
}
