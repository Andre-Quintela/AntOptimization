namespace AntOptimization.Domain.Entities;

public class OptimizationRun
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int LocationCount { get; set; }
    public int? StartLocationIndex { get; set; }
    public string BestRouteOrderJson { get; set; } = string.Empty;
    public double TotalDistanceKm { get; set; }
    public string RouteCoordinatesJson { get; set; } = string.Empty;
}
