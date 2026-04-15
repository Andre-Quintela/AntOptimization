using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Server.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AntOptimization.Tests.Controllers;

public class RouteControllerTests
{
    private static RouteController CreateController(IRouteService? service = null)
    {
        service ??= new Mock<IRouteService>().Object;
        return new RouteController(service);
    }

    private static OptimizationRequest ValidRequest(int locationCount = 2, int? startIndex = null) => new()
    {
        Locations = Enumerable.Range(0, locationCount)
            .Select(i => new LocationDto { Lat = i, Lng = i })
            .ToList(),
        StartLocationIndex = startIndex
    };

    [Fact]
    public async Task Optimize_ReturnsBadRequest_WhenLessThanTwoLocations()
    {
        var controller = CreateController();
        var request = new OptimizationRequest
        {
            Locations = [new LocationDto()]
        };

        var result = await controller.Optimize(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Optimize_ReturnsBadRequest_WhenStartIndexIsNegative()
    {
        var controller = CreateController();
        var request = ValidRequest(locationCount: 3, startIndex: -1);

        var result = await controller.Optimize(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Optimize_ReturnsBadRequest_WhenStartIndexExceedsLocationCount()
    {
        var controller = CreateController();
        var request = ValidRequest(locationCount: 3, startIndex: 3); // valid range: 0–2

        var result = await controller.Optimize(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Optimize_ReturnsOk_WithValidRequest()
    {
        var mockService = new Mock<IRouteService>();
        mockService.Setup(x => x.OptimizeRouteAsync(It.IsAny<OptimizationRequest>()))
                   .ReturnsAsync(new OptimizationResponse
                   {
                       BestRouteOrder = [0, 1],
                       TotalDistance = 1.5
                   });

        var controller = CreateController(mockService.Object);

        var result = await controller.Optimize(ValidRequest());

        result.Result.Should().BeOfType<OkObjectResult>()
              .Which.Value.Should().BeOfType<OptimizationResponse>();
    }

    [Fact]
    public async Task Optimize_ReturnsOk_WithValidStartIndex()
    {
        var mockService = new Mock<IRouteService>();
        mockService.Setup(x => x.OptimizeRouteAsync(It.IsAny<OptimizationRequest>()))
                   .ReturnsAsync(new OptimizationResponse());

        var controller = CreateController(mockService.Object);
        var request = ValidRequest(locationCount: 3, startIndex: 1);

        var result = await controller.Optimize(request);

        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
