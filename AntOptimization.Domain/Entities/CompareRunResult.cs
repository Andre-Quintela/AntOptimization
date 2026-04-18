namespace AntOptimization.Domain.Entities;

public class CompareRunResult
{
    public Guid Id { get; set; }
    public Guid CompareRunId { get; set; }
    public CompareRun CompareRun { get; set; } = null!;
    public string Algorithm { get; set; } = string.Empty;
    public string BestRouteOrderJson { get; set; } = string.Empty;
    public double TotalDistanceKm { get; set; }
    public long ExecutionTimeMs { get; set; }
    public double RelativeGapPercent { get; set; }
    public string RouteCoordinatesJson { get; set; } = string.Empty;
}
