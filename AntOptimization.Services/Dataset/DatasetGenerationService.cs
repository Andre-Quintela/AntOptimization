using AntOptimization.Domain.Algorithms.Exact;
using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.DTOs.Dataset;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services.Dataset;

public class DatasetGenerationService : IDatasetGenerationService
{
    // Rough single-core costs used only for the a-priori estimate shown to the user.
    private const double AcoSecondsPerInnerStep = 40e-9;   // per ant × iteration × n² step
    private const double HeldKarpSecondsPerOperation = 4e-9;
    private const double BruteForceSecondsPerNode = 10e-9;
    private const double OsrmRequestSeconds = 0.5;

    private readonly IDatasetRepository _repository;
    private readonly IDatasetJobQueue _queue;
    private readonly DatasetOptions _options;

    public DatasetGenerationService(IDatasetRepository repository, IDatasetJobQueue queue, DatasetOptions options)
    {
        _repository = repository;
        _queue = queue;
        _options = options;
    }

    public DatasetEstimateResponse Estimate(CreateDatasetJobRequest request)
    {
        var estimate = new DatasetEstimateResponse();
        var errors = estimate.Errors;
        var warnings = estimate.Warnings;

        var region = request.Region.ToBounds();
        if (!region.IsValid)
            errors.Add("Selecione uma região válida no mapa.");

        var pointCounts = NormalizePointCounts(request.PointCounts);
        estimate.PointCounts = pointCounts;

        if (pointCounts.Count == 0)
            errors.Add("Informe ao menos um tamanho de instância (quantidade de pontos).");
        else if (pointCounts[0] < 2)
            errors.Add("Cada tamanho de instância precisa de ao menos 2 pontos.");

        if (request.InstancesPerPointCount < 1)
            errors.Add("A quantidade de instâncias por tamanho deve ser ao menos 1.");

        int totalInstances = pointCounts.Count * Math.Max(0, request.InstancesPerPointCount);
        estimate.TotalInstances = totalInstances;
        if (totalInstances > _options.MaxInstancesPerJob)
            errors.Add($"O total de instâncias (tamanhos × instâncias por tamanho) é {totalInstances} e excede o limite de {_options.MaxInstancesPerJob}.");

        if (request.Repetitions < 1 || request.Repetitions > _options.MaxRepetitions)
            errors.Add($"As repetições por configuração devem estar entre 1 e {_options.MaxRepetitions}.");

        errors.AddRange(request.Grid.Validate());

        var solver = new ExactSolver(_options.ToExactLimits());
        var exactEstimates = new List<ExactCostEstimate>(pointCounts.Count);
        foreach (int n in pointCounts.Where(n => n >= 2))
        {
            var exact = solver.Estimate(n, request.FixedStart);
            if (!exact.IsFeasible)
                errors.Add($"{n} pontos: {exact.Reason}");
            else
                exactEstimates.Add(exact);
        }

        if (errors.Count > 0)
            return estimate;

        var configurations = request.Grid.Expand().ToList();
        // Worst case drives the reported method/operations/memory: the largest instance size.
        var worstExact = exactEstimates[^1];

        estimate.ConfigurationCount = configurations.Count;
        estimate.TotalRuns = totalInstances * configurations.Count * request.Repetitions;
        estimate.ExactMethod = worstExact.Method?.ToString();
        estimate.ExactOperationsPerInstance = worstExact.EstimatedOperations;
        estimate.ExactMemoryBytes = worstExact.EstimatedMemoryBytes;

        // The ACO inner-step count grows with n², so every size is accounted for separately.
        double acoStepsPerSizeSet = pointCounts.Sum(n =>
            configurations.Sum(c => (double)c.Parameters.NumberOfAnts * c.Parameters.Iterations * n * n));
        estimate.EstimatedAcoSeconds = acoStepsPerSizeSet * request.Repetitions *
            request.InstancesPerPointCount * AcoSecondsPerInnerStep;
        estimate.EstimatedExactSeconds = exactEstimates.Sum(e => e.EstimatedOperations *
            (e.Method == ExactMethod.BruteForce ? BruteForceSecondsPerNode : HeldKarpSecondsPerOperation)) *
            request.InstancesPerPointCount;
        estimate.EstimatedNetworkSeconds = request.DistanceSource == DistanceSource.Osrm
            ? totalInstances * (_options.OsrmDelayMs / 1000.0 + OsrmRequestSeconds)
            : 0;
        estimate.EstimatedTotalSeconds = estimate.EstimatedAcoSeconds + estimate.EstimatedExactSeconds + estimate.EstimatedNetworkSeconds;

        if (estimate.TotalRuns > _options.MaxRunsPerJob)
            errors.Add($"Total de {estimate.TotalRuns:N0} execuções do ACO excede o limite de {_options.MaxRunsPerJob:N0}. Reduza a grade, as repetições ou as instâncias.");

        if (estimate.EstimatedTotalSeconds > _options.MaxEstimatedHours * 3600)
            errors.Add($"Tempo estimado ({estimate.EstimatedTotalSeconds / 3600:0.#} h) excede o limite de {_options.MaxEstimatedHours} h.");

        double worstExactSeconds = worstExact.EstimatedOperations *
            (worstExact.Method == ExactMethod.BruteForce ? BruteForceSecondsPerNode : HeldKarpSecondsPerOperation);
        if (worstExactSeconds > _options.ExactTimeoutSeconds)
            warnings.Add($"O cálculo exato das instâncias de {worstExact.Points} pontos pode se aproximar do limite de {_options.ExactTimeoutSeconds}s; instâncias que excederem serão marcadas.");

        var heldKarpSizes = exactEstimates.Where(e => e.Method == ExactMethod.HeldKarp).Select(e => e.Points).ToList();
        if (heldKarpSizes.Count > 0)
            warnings.Add($"Tamanhos {Join(heldKarpSizes)}: a solução ótima será calculada por Held-Karp (programação dinâmica exata); força bruta é usada até {_options.BruteForceMaxPoints} pontos.");

        var trivialSizes = pointCounts.Where(n => n <= 4).ToList();
        if (trivialSizes.Count > 0)
            warnings.Add($"Tamanhos {Join(trivialSizes)}: instâncias com até 4 pontos são triviais, quase todas as configurações atingem o ótimo.");

        if (request.Repetitions == 1)
            warnings.Add("Com apenas 1 repetição não é possível medir a estabilidade do ACO (ele é estocástico).");

        if (pointCounts.Count == 1)
            warnings.Add("Com um único tamanho de instância o dataset não cobre a variação de n; informe uma lista (ex.: 6, 8, 10, 12) para o modelo generalizar.");

        if (request.DistanceSource == DistanceSource.Osrm)
            warnings.Add($"Pontos aleatórios podem cair fora da malha viária; o OSRM os aproxima da via mais próxima e, se não houver rota, a instância é regerada (até {_options.MaxGenerationAttempts} tentativas).");

        if (estimate.EstimatedTotalSeconds > 600)
            warnings.Add("Processamento longo: você pode acompanhar o progresso, cancelar e retomar depois.");

        return estimate;
    }

    /// <summary>Deduplicated, ascending instance sizes - the order the instances are generated in.</summary>
    private static List<int> NormalizePointCounts(IEnumerable<int>? pointCounts) =>
        (pointCounts ?? []).Distinct().Order().ToList();

    private static string Join(IEnumerable<int> values) => string.Join(", ", values);

    public async Task<(DatasetJobDto? Job, DatasetEstimateResponse Estimate)> CreateJobAsync(
        CreateDatasetJobRequest request, CancellationToken cancellationToken = default)
    {
        var estimate = Estimate(request);
        if (!estimate.IsValid)
            return (null, estimate);

        var pointCounts = NormalizePointCounts(request.PointCounts);
        int masterSeed = request.MasterSeed ?? Random.Shared.Next();
        var job = new DatasetJob
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            CreatedAt = DateTime.UtcNow,
            Status = DatasetJobStatus.Pending,
            South = request.Region.South,
            West = request.Region.West,
            North = request.Region.North,
            East = request.Region.East,
            PointCountsJson = DatasetJson.SerializePointCounts(pointCounts),
            InstancesPerPointCount = request.InstancesPerPointCount,
            InstanceCount = estimate.TotalInstances,
            DistanceSource = request.DistanceSource,
            FixedStart = request.FixedStart,
            Repetitions = request.Repetitions,
            MasterSeed = masterSeed,
            GridJson = DatasetJson.SerializeGrid(request.Grid),
            ConfigurationCount = estimate.ConfigurationCount,
            TotalRuns = estimate.TotalRuns
        };

        // Instances are laid out size by size; Index stays contiguous because (JobId, Index) is unique.
        int index = 0;
        foreach (int pointCount in pointCounts)
        {
            for (int replica = 0; replica < request.InstancesPerPointCount; replica++, index++)
            {
                job.Instances.Add(new DatasetInstance
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Index = index,
                    PointCount = pointCount,
                    Seed = SeedDerivation.ForInstance(masterSeed, index),
                    FixedStartIndex = request.FixedStart ? 0 : null
                });
            }
        }

        await _repository.AddJobAsync(job, cancellationToken);
        await _queue.EnqueueAsync(job.Id, cancellationToken);

        return (DatasetMapper.ToDto(job), estimate);
    }

    public async Task<List<DatasetJobDto>> ListJobsAsync(CancellationToken cancellationToken = default) =>
        (await _repository.ListJobsAsync(cancellationToken)).Select(DatasetMapper.ToDto).ToList();

    public async Task<DatasetJobDto?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(jobId, cancellationToken);
        return job is null ? null : DatasetMapper.ToDto(job);
    }

    public async Task<List<DatasetInstanceDto>?> GetInstancesAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (await _repository.GetJobAsync(jobId, cancellationToken) is null)
            return null;

        var instances = await _repository.GetInstancesAsync(jobId, cancellationToken);
        var stats = await _repository.GetInstanceRunStatsAsync(jobId, cancellationToken);
        return instances.Select(i => DatasetMapper.ToDto(i, stats.GetValueOrDefault(i.Id))).ToList();
    }

    public async Task<List<AcoRunDto>?> GetRunsAsync(Guid jobId, Guid instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _repository.GetInstanceAsync(instanceId, cancellationToken);
        if (instance is null || instance.JobId != jobId)
            return null;

        return (await _repository.GetRunsAsync(instanceId, cancellationToken)).Select(DatasetMapper.ToDto).ToList();
    }

    public async Task<List<ConfigSummaryDto>?> GetConfigSummariesAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (await _repository.GetJobAsync(jobId, cancellationToken) is null)
            return null;

        var runs = await _repository.GetRunsForJobAsync(jobId, cancellationToken);
        return DatasetStatistics.SummarizeConfigurations(runs);
    }

    public async Task<DatasetCommandResult> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(jobId, cancellationToken);
        if (job is null)
            return DatasetCommandResult.NotFound;

        if (_queue.RequestCancel(jobId))
            return DatasetCommandResult.Ok; // the runner records the Cancelled status

        if (job.Status is DatasetJobStatus.Pending or DatasetJobStatus.Interrupted)
        {
            job.Status = DatasetJobStatus.Cancelled;
            job.FinishedAt = DateTime.UtcNow;
            await _repository.UpdateJobAsync(job, cancellationToken);
            return DatasetCommandResult.Ok;
        }

        return DatasetCommandResult.InvalidState;
    }

    public async Task<DatasetCommandResult> ResumeAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(jobId, cancellationToken);
        if (job is null)
            return DatasetCommandResult.NotFound;

        bool resumable = job.Status is DatasetJobStatus.Interrupted or DatasetJobStatus.Cancelled
            or DatasetJobStatus.Failed or DatasetJobStatus.CompletedWithErrors;
        if (!resumable || _queue.IsRunning(jobId))
            return DatasetCommandResult.InvalidState;

        job.Status = DatasetJobStatus.Pending;
        job.ErrorMessage = null;
        job.FinishedAt = null;
        await _repository.UpdateJobAsync(job, cancellationToken);
        await _queue.EnqueueAsync(jobId, cancellationToken);
        return DatasetCommandResult.Ok;
    }

    public async Task<DatasetCommandResult> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(jobId, cancellationToken);
        if (job is null)
            return DatasetCommandResult.NotFound;

        if (job.Status == DatasetJobStatus.Running || _queue.IsRunning(jobId))
            return DatasetCommandResult.InvalidState;

        await _repository.DeleteJobAsync(jobId, cancellationToken);
        return DatasetCommandResult.Ok;
    }
}
