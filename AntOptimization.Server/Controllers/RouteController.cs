using System.Text.Json;
using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Interfaces;
using Microsoft.AspNetCore.Http.Features;
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

    [HttpPost("optimize-visual")]
    public async Task OptimizeVisual([FromBody] OptimizationRequest request)
    {
        if (request.Locations.Count < 2)
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("At least 2 locations are required.");
            return;
        }

        if (request.StartLocationIndex.HasValue &&
            (request.StartLocationIndex.Value < 0 || request.StartLocationIndex.Value >= request.Locations.Count))
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("StartLocationIndex is out of range.");
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");

        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        try
        {
            await foreach (var eventData in _routeService.OptimizeRouteVisualAsync(request, HttpContext.RequestAborted))
            {
                var json = JsonSerializer.Serialize(eventData, eventData.GetType(), jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
    }
}
