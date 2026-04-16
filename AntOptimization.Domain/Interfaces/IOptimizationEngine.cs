namespace AntOptimization.Domain.Interfaces;

public interface IOptimizationEngine
{
    string Name { get; }
    (List<int> BestTour, double BestDistance) Solve(double[,] distanceMatrix, int? fixedStartCity = null);
}
