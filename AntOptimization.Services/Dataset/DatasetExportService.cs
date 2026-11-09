using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services.Dataset;

/// <summary>
/// Exports the dataset as ML-friendly CSV (invariant culture, snake_case headers).
/// Every row carries <c>job_id</c> and <c>instance_id</c> so train/test splits can be grouped by instance.
/// </summary>
public partial class DatasetExportService : IDatasetExportService
{
    // PointCount is already an instance key column (and is filled even for failed instances),
    // so it is skipped here to avoid a duplicate "point_count" column in the CSVs.
    private static readonly PropertyInfo[] FeatureProperties = typeof(InstanceFeatures).GetProperties()
        .Where(p => p.Name != nameof(InstanceFeatures.PointCount)).ToArray();
    private static readonly string[] FeatureColumns = FeatureProperties.Select(p => ToSnakeCase(p.Name)).ToArray();

    private static readonly string[] InstanceKeyColumns =
    [
        "job_id", "instance_id", "instance_index", "point_count", "instance_seed", "distance_source", "objective",
        "fixed_start_index", "optimal_distance_m", "exact_method", "exact_time_ms"
    ];

    private static readonly string[] RunColumns =
    [
        "run_id", "config_key", "repetition", "run_seed", "alpha", "beta", "n_ants", "evaporation_rate", "iterations",
        "best_distance_m", "gap_abs_m", "gap_percent", "reached_optimum", "best_iteration", "first_optimal_iteration",
        "tours_constructed", "execution_time_ms", "run_status"
    ];

    private static readonly string[] ConfigColumns =
    [
        "config_key", "alpha", "beta", "n_ants", "evaporation_rate", "iterations", "n_runs",
        "gap_percent_mean", "gap_percent_std", "gap_percent_min", "gap_percent_max",
        "optimal_hit_rate", "execution_time_ms_mean", "best_iteration_mean", "is_best_config"
    ];

    private readonly IDatasetRepository _repository;

    public DatasetExportService(IDatasetRepository repository)
    {
        _repository = repository;
    }

    public async Task WriteAsync(Guid? jobId, DatasetExportLevel level, Stream output, CancellationToken cancellationToken = default)
    {
        if (level == DatasetExportLevel.Zip)
        {
            await WriteZipAsync(jobId, output, cancellationToken);
            return;
        }

        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        var jobs = await LoadJobsAsync(jobId, cancellationToken);

        switch (level)
        {
            case DatasetExportLevel.Runs:
                await WriteRunsAsync(jobId, jobs, writer, cancellationToken);
                break;
            case DatasetExportLevel.Instances:
                await WriteInstancesAsync(jobId, jobs, writer, cancellationToken);
                break;
            case DatasetExportLevel.Configs:
                await WriteConfigsAsync(jobId, jobs, writer, cancellationToken);
                break;
        }

        await writer.FlushAsync(cancellationToken);
    }

    private async Task WriteZipAsync(Guid? jobId, Stream output, CancellationToken cancellationToken)
    {
        // ZipArchive writes synchronously; build it in a temp file, then copy asynchronously.
        string tempPath = Path.GetTempFileName();
        try
        {
            await using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var (name, level) in new[]
                             {
                                 ("runs.csv", DatasetExportLevel.Runs),
                                 ("instances.csv", DatasetExportLevel.Instances),
                                 ("configs_summary.csv", DatasetExportLevel.Configs)
                             })
                    {
                        using var buffer = new MemoryStream();
                        await WriteAsync(jobId, level, buffer, cancellationToken);
                        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                        await using var entryStream = entry.Open();
                        buffer.Position = 0;
                        await buffer.CopyToAsync(entryStream, cancellationToken);
                    }

                    var metadata = archive.CreateEntry("metadata.json");
                    await using var metadataStream = metadata.Open();
                    await JsonSerializer.SerializeAsync(metadataStream, await BuildMetadataAsync(jobId, cancellationToken),
                        new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping },
                        cancellationToken);
                }

                file.Position = 0;
                await file.CopyToAsync(output, cancellationToken);
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private async Task WriteRunsAsync(Guid? jobId, Dictionary<Guid, DatasetJob> jobs, StreamWriter writer, CancellationToken ct)
    {
        await WriteRowAsync(writer, [.. InstanceKeyColumns, .. FeatureColumns, .. RunColumns]);

        await foreach (var instance in _repository.StreamInstancesWithRunsAsync(jobId, ct))
        {
            if (instance.Status != DatasetInstanceStatus.Completed) continue;
            var prefix = InstanceKeyValues(instance, jobs[instance.JobId]).Concat(FeatureValues(instance.Features)).ToArray();

            foreach (var run in instance.Runs)
            {
                await WriteRowAsync(writer,
                [
                    .. prefix,
                    run.Id.ToString(), run.ConfigKey, Format(run.Repetition), Format(run.Seed),
                    Format(run.Alpha), Format(run.Beta), Format(run.NumberOfAnts), Format(run.EvaporationRate), Format(run.Iterations),
                    Format(run.BestDistanceM), Format(run.GapAbsM), Format(run.GapPercent), Format(run.ReachedOptimum),
                    Format(run.BestIteration), Format(run.FirstOptimalIteration), Format(run.ToursConstructed),
                    Format(run.ExecutionTimeMs), run.Status.ToString()
                ]);
            }
        }
    }

    private async Task WriteInstancesAsync(Guid? jobId, Dictionary<Guid, DatasetJob> jobs, StreamWriter writer, CancellationToken ct)
    {
        await WriteRowAsync(writer,
        [
            .. InstanceKeyColumns, "status", "error_message", "generation_attempts", "exact_operations",
            "region_south", "region_west", "region_north", "region_east",
            .. FeatureColumns, "coordinates_json", "optimal_tour_json", "distance_matrix_json"
        ]);

        await foreach (var instance in _repository.StreamInstancesWithRunsAsync(jobId, ct))
        {
            var job = jobs[instance.JobId];
            await WriteRowAsync(writer,
            [
                .. InstanceKeyValues(instance, job),
                instance.Status.ToString(), instance.ErrorMessage ?? "", Format(instance.GenerationAttempts), Format(instance.ExactOperations),
                Format(job.South), Format(job.West), Format(job.North), Format(job.East),
                .. FeatureValues(instance.Features),
                instance.CoordinatesJson ?? "", instance.OptimalTourJson ?? "", instance.DistanceMatrixJson ?? ""
            ]);
        }
    }

    private async Task WriteConfigsAsync(Guid? jobId, Dictionary<Guid, DatasetJob> jobs, StreamWriter writer, CancellationToken ct)
    {
        await WriteRowAsync(writer, [.. InstanceKeyColumns, .. FeatureColumns, .. ConfigColumns]);

        await foreach (var instance in _repository.StreamInstancesWithRunsAsync(jobId, ct))
        {
            if (instance.Status != DatasetInstanceStatus.Completed) continue;
            var prefix = InstanceKeyValues(instance, jobs[instance.JobId]).Concat(FeatureValues(instance.Features)).ToArray();

            var groups = instance.Runs
                .Where(r => r.Status == AcoRunStatus.Completed)
                .GroupBy(r => r.ConfigKey)
                .Select(g => (Runs: g.ToList(), Stats: DatasetStatistics.Aggregate(g.ToList())))
                .ToList();
            if (groups.Count == 0) continue;
            double bestMean = groups.Min(g => g.Stats.MeanGap);

            foreach (var (runs, stats) in groups)
            {
                var first = runs[0];
                await WriteRowAsync(writer,
                [
                    .. prefix,
                    first.ConfigKey, Format(first.Alpha), Format(first.Beta), Format(first.NumberOfAnts),
                    Format(first.EvaporationRate), Format(first.Iterations), Format(stats.RunCount),
                    Format(stats.MeanGap), Format(stats.StdGap), Format(stats.MinGap), Format(stats.MaxGap),
                    Format(stats.OptimalHitRate), Format(stats.MeanTimeMs), Format(stats.MeanBestIteration),
                    Format(stats.MeanGap <= bestMean + 1e-9)
                ]);
            }
        }
    }

    private async Task<object> BuildMetadataAsync(Guid? jobId, CancellationToken ct)
    {
        var jobs = (await LoadJobsAsync(jobId, ct)).Values.OrderBy(j => j.CreatedAt);
        return new
        {
            generatedAtUtc = DateTime.UtcNow,
            objective = AcoConstants.Objective,
            objectiveDescription = "Caminho Hamiltoniano aberto (sem retorno à origem), igual ao ACO da aplicação.",
            distanceUnit = "meters (features in kilometers)",
            gapFormula = "gap_percent = (best_distance_m - optimal_distance_m) / optimal_distance_m * 100",
            acoConstants = new { initialPheromone = AcoConstants.InitialPheromone, depositConstantQ = AcoConstants.DepositConstantQ },
            splitAdvice = "Agrupe por instance_id (ex.: GroupKFold) para que execuções da mesma instância não fiquem em treino e teste.",
            files = new
            {
                runs = "Uma linha por execução do ACO (instâncias concluídas).",
                instances = "Uma linha por instância, incluindo falhas, coordenadas e matriz de custos.",
                configs_summary = "Uma linha por (instância, configuração) com estatísticas sobre as repetições."
            },
            jobs = jobs.Select(j => new
            {
                id = j.Id,
                name = j.Name,
                createdAtUtc = j.CreatedAt,
                status = j.Status.ToString(),
                region = new { j.South, j.West, j.North, j.East },
                pointCounts = DatasetJson.DeserializePointCounts(j.PointCountsJson),
                instancesPerPointCount = j.InstancesPerPointCount,
                instanceCount = j.InstanceCount,
                distanceSource = j.DistanceSource.ToString(),
                fixedStart = j.FixedStart,
                repetitions = j.Repetitions,
                masterSeed = j.MasterSeed,
                grid = DatasetJson.DeserializeGrid(j.GridJson)
            })
        };
    }

    private async Task<Dictionary<Guid, DatasetJob>> LoadJobsAsync(Guid? jobId, CancellationToken ct)
    {
        if (jobId.HasValue)
        {
            var job = await _repository.GetJobAsync(jobId.Value, ct);
            return job is null ? [] : new Dictionary<Guid, DatasetJob> { [job.Id] = job };
        }
        return (await _repository.ListJobsAsync(ct)).ToDictionary(j => j.Id);
    }

    private static IEnumerable<string> InstanceKeyValues(DatasetInstance instance, DatasetJob job) =>
    [
        job.Id.ToString(), instance.Id.ToString(), Format(instance.Index), Format(instance.PointCount), Format(instance.Seed),
        job.DistanceSource.ToString(), AcoConstants.Objective, Format(instance.FixedStartIndex),
        Format(instance.OptimalDistanceM), instance.ExactMethod?.ToString() ?? "", Format(instance.ExactTimeMs)
    ];

    private static IEnumerable<string> FeatureValues(InstanceFeatures? features) =>
        FeatureProperties.Select(p => features is null ? "" : Format(p.GetValue(features)));

    private static string Format(object? value) => value switch
    {
        null => "",
        bool b => b ? "1" : "0",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    private static Task WriteRowAsync(StreamWriter writer, IEnumerable<string> values) =>
        writer.WriteLineAsync(string.Join(",", values.Select(Escape)));

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    internal static string ToSnakeCase(string name) => SnakeCaseRegex().Replace(name, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SnakeCaseRegex();
}
