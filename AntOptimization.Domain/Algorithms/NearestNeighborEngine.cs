namespace AntOptimization.Domain.Algorithms;

public class NearestNeighborEngine
{
    public (List<int> BestTour, double BestDistance) Solve(double[,] distanceMatrix, int? fixedStartCity = null)
    {
        int n = distanceMatrix.GetLength(0);
        int startCity = fixedStartCity ?? 0;

        var visited = new bool[n];
        var tour = new List<int>(n) { startCity };
        visited[startCity] = true;

        for (int step = 1; step < n; step++)
        {
            int current = tour[^1];
            int nearest = -1;
            double nearestDist = double.MaxValue;

            for (int j = 0; j < n; j++)
            {
                if (!visited[j] && distanceMatrix[current, j] < nearestDist)
                {
                    nearestDist = distanceMatrix[current, j];
                    nearest = j;
                }
            }

            tour.Add(nearest);
            visited[nearest] = true;
        }

        double totalDistance = CalculateTourDistance(tour, distanceMatrix);
        return (tour, totalDistance);
    }

    private static double CalculateTourDistance(List<int> tour, double[,] distances)
    {
        double total = 0;
        for (int i = 0; i < tour.Count - 1; i++)
            total += distances[tour[i], tour[i + 1]];
        return total;
    }
}
