namespace AntOptimization.Domain.Interfaces;

public interface INearestNeighborService
{
    (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null);
}
