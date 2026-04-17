namespace AntOptimization.Domain.Interfaces;

public interface IGeneticAlgorithmService
{
    (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null);
}
