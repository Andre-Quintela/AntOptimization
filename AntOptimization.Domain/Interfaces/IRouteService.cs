using AntOptimization.Domain.DTOs;

namespace AntOptimization.Domain.Interfaces;

public interface IRouteService
{
    Task<OptimizationResponse> OptimizeRouteAsync(OptimizationRequest request);
    IAsyncEnumerable<object> OptimizeRouteVisualAsync(OptimizationRequest request, CancellationToken cancellationToken = default);
}
