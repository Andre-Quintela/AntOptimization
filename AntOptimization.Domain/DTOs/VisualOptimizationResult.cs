namespace AntOptimization.Domain.DTOs;

public class VisualOptimizationResult
{
    public string Type => "result";
    public List<int> BestRouteOrder { get; set; } = [];
    public double TotalDistance { get; set; }
    public List<LocationDto> RouteCoordinates { get; set; } = [];
}
