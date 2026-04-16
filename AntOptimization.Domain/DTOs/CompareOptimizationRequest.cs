namespace AntOptimization.Domain.DTOs;

public class CompareOptimizationRequest
{
    public List<LocationDto> Locations { get; set; } = [];
    public int? StartLocationIndex { get; set; }
}
