using System.Diagnostics;
using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Algorithms.Exact;
using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Exceptions;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;

namespace AntOptimization.Services.Dataset;

/// <summary>
/// Processes a dataset job instance by instance:
/// points → cost matrix → features → exact optimum → ACO grid (configs × repetitions) → atomic save.
/// A failing instance is recorded with its reason and does not abort the job.
/// </summary>
public class DatasetJobRunner : IDatasetJobRunner
{
    /// <summary>Relative tolerance used to decide whether a run reached the optimum.</summary>
    public const double OptimumTolerance = 1e-9;

    private static readonly TimeSpan ProgressSaveInterval = TimeSpan.FromSeconds(2);

    private readonly IDatasetRepository _repository;
    private readonly IDatasetCostMatrixProvider _matrixProvider;
    private readonly DatasetOptions _options;
    private DateTime _lastOsrmCall = DateTime.MinValue;

    public DatasetJobRunner(IDatasetRepository repository, IDatasetCostMatrixProvider matrixProvider, DatasetOptions options)
    {
        _repository = repository;
        _matrixProvider = matrixProvider;
        _options = options;
    }

    public async Task RunAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _repository.GetJobAsync(jobId, CancellationToken.None);
        if (job is null || job.Status is not (DatasetJobStatus.Pending or DatasetJobStatus.Interrupted))
            return;

        job.Status = DatasetJobStatus.Running;
        job.StartedAt ??= DateTime.UtcNow;
        job.FinishedAt = null;
        job.ErrorMessage = null;
        await _repository.UpdateJobAsync(job, CancellationToken.None);

        try
        {
            var configurations = DatasetJson.DeserializeGrid(job.GridJson).Expand().ToList();
            var instances = await _repository.GetInstancesAsync(jobId, CancellationToken.None);
            var solver = new ExactSolver(_options.ToExactLimits());

            UpdateProgress(job, instances, inProgressRuns: 0);
            await _repository.UpdateJobAsync(job, CancellationToken.None);

            // Resuming re-processes pending and failed instances; completed ones are kept.
            foreach (var instance in instances.Where(i => i.Status is DatasetInstanceStatus.Pending or DatasetInstanceStatus.Failed))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var runs = await ProcessInstanceAsync(job, instance, configurations, solver, instances, cancellationToken);
                await _repository.SaveInstanceResultAsync(instance, runs, CancellationToken.None);

                UpdateProgress(job, instances, inProgressRuns: 0);
                await _repository.UpdateJobAsync(job, CancellationToken.None);
            }

            int completed = instances.Count(i => i.Status == DatasetInstanceStatus.Completed);
            job.Status = completed == instances.Count ? DatasetJobStatus.Completed
                : completed == 0 ? DatasetJobStatus.Failed
                : DatasetJobStatus.CompletedWithErrors;
            if (job.Status == DatasetJobStatus.Failed)
                job.ErrorMessage = "Nenhuma instância pôde ser processada. Verifique os erros das instâncias.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Status = DatasetJobStatus.Cancelled;
            job.ErrorMessage = "Processamento cancelado. Instâncias concluídas foram mantidas.";
        }
        catch (Exception ex)
        {
            job.Status = DatasetJobStatus.Failed;
            job.ErrorMessage = $"Falha inesperada: {ex.Message}";
        }

        job.FinishedAt = DateTime.UtcNow;
        await _repository.UpdateJobAsync(job, CancellationToken.None);
    }

    private async Task<List<AcoRun>> ProcessInstanceAsync(
        DatasetJob job,
        DatasetInstance instance,
        List<GridConfiguration> configurations,
        ExactSolver solver,
        List<DatasetInstance> allInstances,
        CancellationToken cancellationToken)
    {
        ResetInstance(instance, job);

        var region = new GeoBounds(job.South, job.West, job.North, job.East);
        var (points, matrix) = await GenerateInstanceAsync(job, instance, region, cancellationToken);
        if (points is null || matrix is null)
            return [];

        instance.CoordinatesJson = DatasetJson.SerializeLocations(points);
        instance.DistanceMatrixJson = DatasetJson.SerializeMatrix(matrix);
        instance.Features = InstanceFeatureExtractor.Extract(points, matrix, region, instance.FixedStartIndex);

        var exact = await SolveExactAsync(instance, matrix, solver, cancellationToken);
        if (exact is null)
            return [];

        instance.OptimalDistanceM = RoundMeters(exact.Distance);
        instance.OptimalTourJson = DatasetJson.SerializeTour(exact.Tour);
        instance.ExactMethod = exact.Method;
        instance.ExactOperations = exact.Operations;
        InstanceFeatureExtractor.ApplyOptimum(instance.Features, instance.OptimalDistanceM.Value);

        if (ValidateBaselines(instance) is string baselineError)
        {
            instance.Status = DatasetInstanceStatus.Failed;
            instance.ErrorMessage = baselineError;
            instance.ProcessedAt = DateTime.UtcNow;
            return [];
        }

        var runs = new List<AcoRun>(configurations.Count * job.Repetitions);
        var lastProgressSave = DateTime.UtcNow;

        foreach (var configuration in configurations)
        {
            for (int repetition = 0; repetition < job.Repetitions; repetition++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                runs.Add(ExecuteRun(instance, configuration, repetition, matrix, instance.OptimalDistanceM.Value));

                if (DateTime.UtcNow - lastProgressSave > ProgressSaveInterval)
                {
                    UpdateProgress(job, allInstances, runs.Count);
                    await _repository.UpdateJobAsync(job, CancellationToken.None);
                    lastProgressSave = DateTime.UtcNow;
                }
            }
        }

        instance.Status = DatasetInstanceStatus.Completed;
        instance.ProcessedAt = DateTime.UtcNow;
        return runs;
    }

    private async Task<(List<Location>? Points, double[,]? Matrix)> GenerateInstanceAsync(
        DatasetJob job, DatasetInstance instance, GeoBounds region, CancellationToken cancellationToken)
    {
        string? lastError = null;
        int attempts = Math.Max(1, _options.MaxGenerationAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int seed = SeedDerivation.ForInstance(job.MasterSeed, instance.Index, attempt);
            var points = RandomPointGenerator.Generate(region, instance.PointCount, seed);
            instance.Seed = seed;
            instance.GenerationAttempts = attempt + 1;

            try
            {
                if (job.DistanceSource == DistanceSource.Osrm)
                    await ThrottleOsrmAsync(cancellationToken);

                var matrix = await _matrixProvider.GetMatrixAsync(points, job.DistanceSource, cancellationToken);
                ValidateMatrix(matrix, points.Count);
                RoundMatrix(matrix);
                return (points, matrix);
            }
            catch (UnreachableLocationException ex)
            {
                lastError = ex.Message;
            }
            catch (HttpRequestException ex)
            {
                lastError = $"Falha ao consultar o OSRM: {ex.Message}";
            }
            catch (InvalidDataException ex)
            {
                lastError = ex.Message;
            }
        }

        instance.Status = DatasetInstanceStatus.Failed;
        instance.ErrorMessage = $"Não foi possível gerar a matriz de custos após {attempts} tentativa(s). Último erro: {lastError}";
        instance.ProcessedAt = DateTime.UtcNow;
        return (null, null);
    }

    private async Task<ExactSolution?> SolveExactAsync(
        DatasetInstance instance, double[,] matrix, ExactSolver solver, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ExactTimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var solution = await Task.Run(() => solver.Solve(matrix, instance.FixedStartIndex, timeout.Token), timeout.Token);
            instance.ExactTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            return solution;
        }
        catch (ExactLimitExceededException ex)
        {
            MarkExceeded(instance, ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            MarkExceeded(instance, $"O cálculo da solução ótima excedeu o tempo limite de {_options.ExactTimeoutSeconds}s.");
            instance.ExactTimeMs = stopwatch.Elapsed.TotalMilliseconds;
        }

        return null;
    }

    private static AcoRun ExecuteRun(
        DatasetInstance instance, GridConfiguration configuration, int repetition, double[,] matrix, double optimalDistance)
    {
        var p = configuration.Parameters;
        int seed = SeedDerivation.ForRun(instance.Seed, configuration.Key, repetition);
        var run = new AcoRun
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            ConfigKey = configuration.Key,
            Repetition = repetition,
            Seed = seed,
            Alpha = p.Alpha,
            Beta = p.Beta,
            NumberOfAnts = p.NumberOfAnts,
            EvaporationRate = p.EvaporationRate,
            Iterations = p.Iterations
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var metrics = new ACOEngine(p, seed).SolveWithMetrics(matrix, instance.FixedStartIndex);
            stopwatch.Stop();

            double tolerance = OptimumTolerance * Math.Max(1.0, optimalDistance);
            run.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            run.BestDistanceM = RoundMeters(metrics.BestDistance);
            run.GapAbsM = RoundMeters(run.BestDistanceM - optimalDistance);
            run.GapPercent = InstanceFeatureExtractor.GapPercent(run.BestDistanceM, optimalDistance);
            run.ReachedOptimum = run.GapAbsM <= tolerance;
            run.BestIteration = metrics.BestIteration;
            run.FirstOptimalIteration = FirstIndexAtOrBelow(metrics.ConvergenceCurve, optimalDistance + tolerance);
            run.ToursConstructed = metrics.ToursConstructed;
            run.BestTourJson = DatasetJson.SerializeTour(metrics.BestTour);

            // Quality check: an ACO tour can never beat the exact optimum on the same matrix.
            if (run.GapAbsM < -tolerance)
            {
                run.Status = AcoRunStatus.Failed;
                run.ErrorMessage = "Inconsistência: distância do ACO menor que a solução ótima.";
            }
            else
            {
                run.Status = AcoRunStatus.Completed;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Status = AcoRunStatus.Failed;
            run.ErrorMessage = ex.Message;
        }

        return run;
    }

    private async Task ThrottleOsrmAsync(CancellationToken cancellationToken)
    {
        var wait = _lastOsrmCall + TimeSpan.FromMilliseconds(_options.OsrmDelayMs) - DateTime.UtcNow;
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, cancellationToken);
        _lastOsrmCall = DateTime.UtcNow;
    }

    private static void ResetInstance(DatasetInstance instance, DatasetJob job)
    {
        instance.Status = DatasetInstanceStatus.Pending;
        instance.ErrorMessage = null;
        instance.ProcessedAt = null;
        instance.FixedStartIndex = job.FixedStart ? 0 : null;
        instance.CoordinatesJson = null;
        instance.DistanceMatrixJson = null;
        instance.Features = null;
        instance.OptimalDistanceM = null;
        instance.OptimalTourJson = null;
        instance.ExactMethod = null;
        instance.ExactTimeMs = null;
        instance.ExactOperations = null;
    }

    /// <summary>
    /// A constructive baseline solves the same open-path objective on the same matrix, so it can
    /// never beat the exact optimum. A violation means the baseline reports a distance that is not
    /// the cost of its own route (as a 2-opt gain formula ignoring reversed inner edges once did),
    /// and such a value must never reach the dataset silently.
    /// </summary>
    private static string? ValidateBaselines(DatasetInstance instance)
    {
        double optimum = instance.OptimalDistanceM!.Value;
        double tolerance = OptimumTolerance * Math.Max(1.0, optimum);
        var features = instance.Features!;

        if (features.NearestNeighborDistanceM < optimum - tolerance)
            return $"Inconsist\u00eancia: Nearest Neighbor ({features.NearestNeighborDistanceM:0.###} m) menor que a solu\u00e7\u00e3o \u00f3tima ({optimum:0.###} m).";

        if (features.TwoOptDistanceM < optimum - tolerance)
            return $"Inconsist\u00eancia: 2-opt ({features.TwoOptDistanceM:0.###} m) menor que a solu\u00e7\u00e3o \u00f3tima ({optimum:0.###} m).";

        return null;
    }

    private static void MarkExceeded(DatasetInstance instance, string message)
    {
        instance.Status = DatasetInstanceStatus.ExceededLimit;
        instance.ErrorMessage = message;
        instance.ProcessedAt = DateTime.UtcNow;
    }

    private static void ValidateMatrix(double[,] matrix, int expectedSize)
    {
        if (matrix.GetLength(0) != expectedSize || matrix.GetLength(1) != expectedSize)
            throw new InvalidDataException("Matriz de custos com dimensão inesperada.");

        foreach (double value in matrix)
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new InvalidDataException("Matriz de custos contém valores inválidos.");
    }

    /// <summary>
    /// Rounds to millimeters so the matrix stored in the dataset is bit-for-bit
    /// the matrix both solvers used.
    /// </summary>
    private static void RoundMatrix(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                matrix[i, j] = Math.Round(matrix[i, j], 3);
    }

    /// <summary>
    /// Path sums of a millimeter-rounded matrix carry floating-point noise (e.g. 14047.800000000001);
    /// rounding to millimeters keeps stored and exported distances exact and comparable.
    /// </summary>
    private static double RoundMeters(double meters) => Math.Round(meters, 3);

    private static int? FirstIndexAtOrBelow(double[] curve, double threshold)
    {
        for (int i = 0; i < curve.Length; i++)
            if (curve[i] <= threshold) return i;
        return null;
    }

    private static void UpdateProgress(DatasetJob job, List<DatasetInstance> instances, int inProgressRuns)
    {
        int runsPerInstance = job.ConfigurationCount * job.Repetitions;
        int completed = instances.Count(i => i.Status == DatasetInstanceStatus.Completed);
        job.ProcessedInstances = instances.Count(i => i.Status != DatasetInstanceStatus.Pending);
        job.FailedInstances = instances.Count(i => i.Status is DatasetInstanceStatus.Failed or DatasetInstanceStatus.ExceededLimit);
        job.CompletedRuns = completed * runsPerInstance + inProgressRuns;
    }
}
