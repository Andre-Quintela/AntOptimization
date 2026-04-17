namespace AntOptimization.Domain.Interfaces;

public interface ITwoOptService
{
    (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null);
}
