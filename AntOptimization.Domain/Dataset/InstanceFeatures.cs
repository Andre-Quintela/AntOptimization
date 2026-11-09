namespace AntOptimization.Domain.Dataset;

/// <summary>
/// Descriptive characteristics of a TSP instance, used as input features for the ML model.
/// Spatial measures use straight-line (Haversine) distances; "Cost" measures use the cost
/// matrix actually given to the solvers (OSRM or Haversine). All lengths are in kilometers.
/// </summary>
public class InstanceFeatures
{
    public int PointCount { get; set; }

    // Region / spatial distribution
    public double RegionWidthKm { get; set; }
    public double RegionHeightKm { get; set; }
    public double RegionAreaKm2 { get; set; }
    public double PointDensityPerKm2 { get; set; }
    public double CentroidDistanceMeanKm { get; set; }
    public double CentroidDistanceStdKm { get; set; }
    public double SpatialNearestNeighborMeanKm { get; set; }
    /// <summary>Clark-Evans aggregation index: &lt;1 clustered, ≈1 random, &gt;1 regular.</summary>
    public double ClarkEvansRatio { get; set; }

    // Cost matrix statistics (off-diagonal entries)
    public double CostMeanKm { get; set; }
    public double CostStdKm { get; set; }
    public double CostMinKm { get; set; }
    public double CostMaxKm { get; set; }
    public double CostMedianKm { get; set; }
    public double CostCoefficientOfVariation { get; set; }
    public double CostNearestNeighborMeanKm { get; set; }
    public double CostNearestNeighborStdKm { get; set; }
    /// <summary>Mean |d(i,j) − d(j,i)| divided by the mean cost (0 for symmetric matrices).</summary>
    public double AsymmetryIndex { get; set; }
    /// <summary>Mean cost / mean straight-line distance (1 for Haversine; &gt;1 for road networks).</summary>
    public double DetourIndex { get; set; }

    // Cheap constructive baselines (meters, same objective as the ACO)
    public double NearestNeighborDistanceM { get; set; }
    public double TwoOptDistanceM { get; set; }
    public double? NearestNeighborGapPercent { get; set; }
    public double? TwoOptGapPercent { get; set; }
}
