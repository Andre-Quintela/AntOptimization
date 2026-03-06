namespace AntOptimization.Domain.Interfaces;

public interface IAntColonyOptimizationService
{
    (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null);
}
