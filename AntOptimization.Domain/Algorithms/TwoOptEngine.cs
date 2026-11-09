namespace AntOptimization.Domain.Algorithms;

public class TwoOptEngine
{
    /// <summary>
    /// Safety bound on the improvement passes. With exact deltas every applied move strictly
    /// lowers the path cost, so the loop terminates on its own; the cap only guards against
    /// pathological floating-point ties.
    /// </summary>
    private const int MaxPasses = 1000;

    /// <summary>Relative tolerance: a move must improve by more than this to be applied.</summary>
    private const double RelativeTolerance = 1e-9;

    public (List<int> BestTour, double BestDistance) Solve(double[,] distanceMatrix, int? fixedStartCity = null)
    {
        // Start with a Nearest Neighbor tour
        var nn = new NearestNeighborEngine();
        var (tour, _) = nn.Solve(distanceMatrix, fixedStartCity);

        double distance = PathMath.PathDistance(tour, distanceMatrix);
        int n = tour.Count;

        for (int pass = 0; pass < MaxPasses; pass++)
        {
            bool improved = false;
            double tolerance = RelativeTolerance * Math.Max(1.0, distance);

            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    double delta = TwoOptDelta(tour, distanceMatrix, i, j);
                    if (delta < -tolerance)
                    {
                        ReverseSegment(tour, i + 1, j);
                        distance += delta;
                        improved = true;
                    }
                }
            }

            if (!improved) break;
        }

        // Recomputed from the final tour so the reported distance is always the cost of this route.
        return (tour, PathMath.PathDistance(tour, distanceMatrix));
    }

    /// <summary>
    /// Exact cost change of reversing <c>tour[i+1..j]</c> under the open-path objective.
    /// Besides the two boundary edges, the reversal flips every edge inside the segment; on an
    /// asymmetric matrix (OSRM road distances) d[y,x] != d[x,y], so those edges change cost too.
    /// Ignoring them both corrupts the running distance and lets a move and its inverse look
    /// improving forever.
    /// </summary>
    private static double TwoOptDelta(List<int> tour, double[,] distances, int i, int j)
    {
        int a = tour[i], b = tour[i + 1];
        int c = tour[j], d = j + 1 < tour.Count ? tour[j + 1] : -1;

        double removed = distances[a, b] + (d >= 0 ? distances[c, d] : 0);
        double added = distances[a, c] + (d >= 0 ? distances[b, d] : 0);

        double reversedInside = 0;
        for (int k = i + 1; k < j; k++)
            reversedInside += distances[tour[k + 1], tour[k]] - distances[tour[k], tour[k + 1]];

        return added - removed + reversedInside;
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
