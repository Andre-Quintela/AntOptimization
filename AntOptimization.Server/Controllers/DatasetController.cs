using AntOptimization.Domain.DTOs.Dataset;
using AntOptimization.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AntOptimization.Server.Controllers;

[ApiController]
[Route("api/datasets")]
public class DatasetController : ControllerBase
{
    private readonly IDatasetGenerationService _datasetService;
    private readonly IDatasetExportService _exportService;

    public DatasetController(IDatasetGenerationService datasetService, IDatasetExportService exportService)
    {
        _datasetService = datasetService;
        _exportService = exportService;
    }

    [HttpPost("estimate")]
    public ActionResult<DatasetEstimateResponse> Estimate([FromBody] CreateDatasetJobRequest request) =>
        Ok(_datasetService.Estimate(request));

    [HttpPost("jobs")]
    public async Task<ActionResult<DatasetJobDto>> CreateJob([FromBody] CreateDatasetJobRequest request, CancellationToken cancellationToken)
    {
        var (job, estimate) = await _datasetService.CreateJobAsync(request, cancellationToken);
        if (job is null)
            return BadRequest(estimate);

        return AcceptedAtAction(nameof(GetJob), new { id = job.Id }, job);
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<List<DatasetJobDto>>> ListJobs(CancellationToken cancellationToken) =>
        Ok(await _datasetService.ListJobsAsync(cancellationToken));

    [HttpGet("jobs/{id:guid}")]
    public async Task<ActionResult<DatasetJobDto>> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await _datasetService.GetJobAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("jobs/{id:guid}/instances")]
    public async Task<ActionResult<List<DatasetInstanceDto>>> GetInstances(Guid id, CancellationToken cancellationToken)
    {
        var instances = await _datasetService.GetInstancesAsync(id, cancellationToken);
        return instances is null ? NotFound() : Ok(instances);
    }

    [HttpGet("jobs/{id:guid}/instances/{instanceId:guid}/runs")]
    public async Task<ActionResult<List<AcoRunDto>>> GetRuns(Guid id, Guid instanceId, CancellationToken cancellationToken)
    {
        var runs = await _datasetService.GetRunsAsync(id, instanceId, cancellationToken);
        return runs is null ? NotFound() : Ok(runs);
    }

    [HttpGet("jobs/{id:guid}/configs")]
    public async Task<ActionResult<List<ConfigSummaryDto>>> GetConfigSummaries(Guid id, CancellationToken cancellationToken)
    {
        var summaries = await _datasetService.GetConfigSummariesAsync(id, cancellationToken);
        return summaries is null ? NotFound() : Ok(summaries);
    }

    [HttpPost("jobs/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await _datasetService.CancelAsync(id, cancellationToken), "O job não está em execução nem na fila.");

    [HttpPost("jobs/{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await _datasetService.ResumeAsync(id, cancellationToken), "Somente jobs interrompidos, cancelados ou com falhas podem ser retomados.");

    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await _datasetService.DeleteAsync(id, cancellationToken), "Cancele o job antes de excluí-lo.");

    [HttpGet("jobs/{id:guid}/export")]
    public async Task ExportJob(Guid id, [FromQuery] DatasetExportLevel level = DatasetExportLevel.Runs, CancellationToken cancellationToken = default)
    {
        if (await _datasetService.GetJobAsync(id, cancellationToken) is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await WriteExportAsync(id, level, $"dataset_{id:N}", cancellationToken);
    }

    [HttpGet("export")]
    public Task ExportAll([FromQuery] DatasetExportLevel level = DatasetExportLevel.Runs, CancellationToken cancellationToken = default) =>
        WriteExportAsync(null, level, "dataset_all", cancellationToken);

    private async Task WriteExportAsync(Guid? jobId, DatasetExportLevel level, string baseName, CancellationToken cancellationToken)
    {
        var (extension, contentType) = level == DatasetExportLevel.Zip
            ? ("zip", "application/zip")
            : ("csv", "text/csv; charset=utf-8");
        string suffix = level == DatasetExportLevel.Zip ? "" : $"_{level.ToString().ToLowerInvariant()}";

        Response.ContentType = contentType;
        Response.Headers.ContentDisposition = $"attachment; filename=\"{baseName}{suffix}.{extension}\"";
        await _exportService.WriteAsync(jobId, level, Response.Body, cancellationToken);
    }

    private IActionResult ToActionResult(DatasetCommandResult result, string invalidStateMessage) => result switch
    {
        DatasetCommandResult.Ok => NoContent(),
        DatasetCommandResult.NotFound => NotFound(),
        _ => Conflict(invalidStateMessage)
    };
}
