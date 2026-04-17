namespace AntOptimization.Domain.DTOs;

public class AlgorithmResult
{
    public string Algorithm { get; set; } = string.Empty;
    public List<int> BestRouteOrder { get; set; } = [];
    public double TotalDistance { get; set; }
    public long ExecutionTimeMs { get; set; }
    public double RelativeGapPercent { get; set; } = 0.0;
    public List<LocationDto> RouteCoordinates { get; set; } = [];
}
