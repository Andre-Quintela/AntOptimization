namespace AntOptimization.Domain.DTOs;

public class OptimizationResponse
{
    public List<int> BestRouteOrder { get; set; } = [];
    public double TotalDistance { get; set; }
    public List<LocationDto> RouteCoordinates { get; set; } = [];
}
