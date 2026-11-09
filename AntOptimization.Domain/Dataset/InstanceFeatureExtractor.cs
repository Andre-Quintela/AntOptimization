using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Models;

namespace AntOptimization.Domain.Dataset;

public static class InstanceFeatureExtractor
{
    public static InstanceFeatures Extract(
        IReadOnlyList<Location> locations,
        double[,] costMatrix,
        GeoBounds region,
        int? fixedStartCity)
    {
        int n = locations.Count;
        var straight = HaversineDistance.Matrix(locations);

        double centroidLat = locations.Average(l => l.Lat);
        double centroidLng = locations.Average(l => l.Lng);
        var centroidDistances = locations
            .Select(l => HaversineDistance.Meters(l.Lat, l.Lng, centroidLat, centroidLng) / 1000.0)
            .ToList();

        var spatialNn = NearestNeighborDistances(straight).Select(m => m / 1000.0).ToList();
        var costNn = NearestNeighborDistances(costMatrix).Select(m => m / 1000.0).ToList();

        var costs = new List<double>(n * (n - 1));
        var straightValues = new List<double>(n * (n - 1));
        double asymmetrySum = 0;
        int pairCount = 0;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;
                costs.Add(costMatrix[i, j] / 1000.0);
                straightValues.Add(straight[i, j] / 1000.0);
                if (i < j)
                {
                    asymmetrySum += Math.Abs(costMatrix[i, j] - costMatrix[j, i]) / 1000.0;
                    pairCount++;
                }
            }

        double area = region.AreaKm2;
        double costMean = Mean(costs);
        double costStd = Std(costs);
        double spatialNnMean = Mean(spatialNn);
        double expectedNn = n > 0 && area > 0 ? 0.5 / Math.Sqrt(n / area) : 0;
        double straightMean = Mean(straightValues);

        var (_, nnDistance) = new NearestNeighborEngine().Solve(costMatrix, fixedStartCity);
        var (_, twoOptDistance) = new TwoOptEngine().Solve(costMatrix, fixedStartCity);

        return new InstanceFeatures
        {
            PointCount = n,
            RegionWidthKm = region.WidthKm,
            RegionHeightKm = region.HeightKm,
            RegionAreaKm2 = area,
            PointDensityPerKm2 = area > 0 ? n / area : 0,
            CentroidDistanceMeanKm = Mean(centroidDistances),
            CentroidDistanceStdKm = Std(centroidDistances),
            SpatialNearestNeighborMeanKm = spatialNnMean,
            ClarkEvansRatio = expectedNn > 0 ? spatialNnMean / expectedNn : 0,
            CostMeanKm = costMean,
            CostStdKm = costStd,
            CostMinKm = costs.Count > 0 ? costs.Min() : 0,
            CostMaxKm = costs.Count > 0 ? costs.Max() : 0,
            CostMedianKm = Median(costs),
            CostCoefficientOfVariation = costMean > 0 ? costStd / costMean : 0,
            CostNearestNeighborMeanKm = Mean(costNn),
            CostNearestNeighborStdKm = Std(costNn),
            AsymmetryIndex = pairCount > 0 && costMean > 0 ? asymmetrySum / pairCount / costMean : 0,
            DetourIndex = straightMean > 0 ? costMean / straightMean : 0,
            NearestNeighborDistanceM = Math.Round(nnDistance, 3),
            TwoOptDistanceM = Math.Round(twoOptDistance, 3)
        };
    }

    /// <summary>Fills the baseline gaps once the optimal distance is known.</summary>
    public static void ApplyOptimum(InstanceFeatures features, double optimalDistance)
    {
        features.NearestNeighborGapPercent = GapPercent(features.NearestNeighborDistanceM, optimalDistance);
        features.TwoOptGapPercent = GapPercent(features.TwoOptDistanceM, optimalDistance);
    }

    public static double GapPercent(double distance, double optimalDistance) =>
        optimalDistance > 0 ? (distance - optimalDistance) / optimalDistance * 100.0 : 0.0;

    private static List<double> NearestNeighborDistances(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        var result = new List<double>(n);
        for (int i = 0; i < n; i++)
        {
            double min = double.MaxValue;
            for (int j = 0; j < n; j++)
                if (i != j && matrix[i, j] < min) min = matrix[i, j];
            if (min < double.MaxValue) result.Add(min);
        }
        return result;
    }

    private static double Mean(IReadOnlyCollection<double> values) => values.Count > 0 ? values.Average() : 0;

    private static double Std(IReadOnlyCollection<double> values)
    {
        if (values.Count < 2) return 0;
        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }
}
