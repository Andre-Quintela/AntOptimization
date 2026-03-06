namespace AntOptimization.Domain.DTOs;

public class OptimizationRequest
{
    public List<LocationDto> Locations { get; set; } = [];
    public int? StartLocationIndex { get; set; }
}
