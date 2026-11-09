using AntOptimization.Domain.DTOs.Dataset;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Server.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AntOptimization.Tests.Controllers;

public class DatasetControllerTests
{
    private readonly Mock<IDatasetGenerationService> _service = new();
    private readonly Mock<IDatasetExportService> _export = new();
    private DatasetController Controller => new(_service.Object, _export.Object);

    [Fact]
    public async Task CreateJob_InvalidRequest_ReturnsBadRequestWithErrors()
    {
        var estimate = new DatasetEstimateResponse { Errors = ["Selecione uma região válida no mapa."] };
        _service.Setup(s => s.CreateJobAsync(It.IsAny<CreateDatasetJobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((DatasetJobDto?)null, estimate));

        var result = await Controller.CreateJob(new CreateDatasetJobRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be(estimate);
    }

    [Fact]
    public async Task CreateJob_Valid_ReturnsAccepted()
    {
        var job = new DatasetJobDto { Id = Guid.NewGuid() };
        _service.Setup(s => s.CreateJobAsync(It.IsAny<CreateDatasetJobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((job, new DatasetEstimateResponse()));

        var result = await Controller.CreateJob(new CreateDatasetJobRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<AcceptedAtActionResult>().Which.Value.Should().Be(job);
    }

    [Fact]
    public async Task GetJob_Unknown_ReturnsNotFound()
    {
        _service.Setup(s => s.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((DatasetJobDto?)null);

        var result = await Controller.GetJob(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(DatasetCommandResult.Ok, typeof(NoContentResult))]
    [InlineData(DatasetCommandResult.NotFound, typeof(NotFoundResult))]
    [InlineData(DatasetCommandResult.InvalidState, typeof(ConflictObjectResult))]
    public async Task Commands_MapResultsToStatusCodes(DatasetCommandResult outcome, Type expected)
    {
        _service.Setup(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(outcome);
        _service.Setup(s => s.ResumeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(outcome);
        _service.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(outcome);

        (await Controller.Cancel(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType(expected);
        (await Controller.Resume(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType(expected);
        (await Controller.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType(expected);
    }

    [Fact]
    public void Estimate_ReturnsServiceEstimate()
    {
        var estimate = new DatasetEstimateResponse { TotalRuns = 10 };
        _service.Setup(s => s.Estimate(It.IsAny<CreateDatasetJobRequest>())).Returns(estimate);

        Controller.Estimate(new CreateDatasetJobRequest()).Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(estimate);
    }
}
