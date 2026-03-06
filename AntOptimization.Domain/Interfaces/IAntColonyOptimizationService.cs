using AntOptimization.Domain.Algorithms;

namespace AntOptimization.Domain.Interfaces;

public interface IAntColonyOptimizationService
{
    (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null);
    (List<int> BestTour, double BestDistance, List<IterationSnapshot> History) OptimizeWithHistory(double[,] distanceMatrix, int? startCity = null);
}
