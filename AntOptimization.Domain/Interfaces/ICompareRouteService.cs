using AntOptimization.Domain.DTOs;

namespace AntOptimization.Domain.Interfaces;

public interface ICompareRouteService
{
    Task<CompareOptimizationResponse> CompareRoutesAsync(CompareOptimizationRequest request);
}
