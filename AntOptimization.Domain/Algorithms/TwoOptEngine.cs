namespace AntOptimization.Domain.Algorithms;

public class TwoOptEngine
{
    public (List<int> BestTour, double BestDistance) Solve(double[,] distanceMatrix, int? fixedStartCity = null)
    {
        // Start with a Nearest Neighbor tour
        var nn = new NearestNeighborEngine();
        var (tour, distance) = nn.Solve(distanceMatrix, fixedStartCity);

        bool improved = true;
        while (improved)
        {
            improved = false;
            int n = tour.Count;

            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    double delta = TwoOptDelta(tour, distanceMatrix, i, j);
                    if (delta < -1e-10)
                    {
                        ReverseSegment(tour, i + 1, j);
                        distance += delta;
                        improved = true;
                    }
                }
            }
        }

        return (tour, distance);
    }

    private static double TwoOptDelta(List<int> tour, double[,] distances, int i, int j)
    {
        int a = tour[i], b = tour[i + 1];
        int c = tour[j], d = j + 1 < tour.Count ? tour[j + 1] : -1;

        double removed = distances[a, b] + (d >= 0 ? distances[c, d] : 0);
        double added = distances[a, c] + (d >= 0 ? distances[b, d] : 0);
        return added - removed;
    }

    private static void ReverseSegment(List<int> tour, int left, int right)
    {
        while (left < right)
        {
            (tour[left], tour[right]) = (tour[right], tour[left]);
            left++;
            right--;
        }
    }
}
