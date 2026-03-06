using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services;

public class AntColonyOptimizationService : IAntColonyOptimizationService
{
    public (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null)
    {
        var engine = new ACOEngine(DefaultParameters);
        return engine.Solve(distanceMatrix, startCity);
    }

    public (List<int> BestTour, double BestDistance, List<IterationSnapshot> History) OptimizeWithHistory(double[,] distanceMatrix, int? startCity = null)
    {
        var engine = new ACOEngine(DefaultParameters);
        return engine.SolveWithHistory(distanceMatrix, startCity);
    }

    private static ACOParameters DefaultParameters => new()
    {
        NumberOfAnts = 20,
        EvaporationRate = 0.5,
        Alpha = 1.0,
        Beta = 2.0,
        Iterations = 100
    };
}
