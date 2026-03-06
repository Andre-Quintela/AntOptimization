using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AntOptimization.Server.Controllers;

[ApiController]
[Route("api/routes")]
public class RouteController : ControllerBase
{
    private readonly IRouteService _routeService;

    public RouteController(IRouteService routeService)
    {
        _routeService = routeService;
    }

    [HttpPost("optimize")]
    public async Task<ActionResult<OptimizationResponse>> Optimize([FromBody] OptimizationRequest request)
    {
        if (request.Locations.Count < 2)
            return BadRequest("At least 2 locations are required.");

        if (request.StartLocationIndex.HasValue &&
            (request.StartLocationIndex.Value < 0 || request.StartLocationIndex.Value >= request.Locations.Count))
            return BadRequest("StartLocationIndex is out of range.");

        var result = await _routeService.OptimizeRouteAsync(request);
        return Ok(result);
    }
}
