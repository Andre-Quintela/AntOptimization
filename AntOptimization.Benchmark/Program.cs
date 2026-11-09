using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Algorithms.Exact;
using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.Exceptions;
using AntOptimization.Domain.Models;
using AntOptimization.Infrastructure.Services;

namespace AntOptimization.Benchmark;

/// <summary>
/// Comparison experiment of the four optimization algorithms (section 3.7 of the article).
///
/// For every instance size, the same random instances are solved by Nearest Neighbor, 2-Opt, ACO and
/// the Genetic Algorithm over the same cost matrix, measuring distance and wall-clock time. Points,
/// ACO seeds and GA seeds all derive from one master seed, so the whole experiment is reproducible.
///
/// Usage (from the repository root):
///   dotnet run -c Release --project AntOptimization.Benchmark -- --out datasets/comparacao
/// </summary>
public static class Program
{
    /// <summary>Grande Vitória (ES) — the same region used by the hyperparameter dataset.</summary>
    private static readonly GeoBounds DefaultRegion = new(South: -20.39, West: -40.39, North: -20.24, East: -40.265);

    public static async Task<int> Main(string[] args)
    {
        var options = BenchmarkOptions.Parse(args);
        if (options is null)
        {
            Console.WriteLine(BenchmarkOptions.Usage);
            return 1;
        }

        Directory.CreateDirectory(options.OutputDirectory);
        PrintHeader(options);

        using var http = CreateOsrmClient();
        var exactSolver = new ExactSolver(new ExactSolverLimits
        {
            BruteForceMaxPoints = options.BruteForceMaxPoints,
            MaxExactPoints = options.MaxExactPoints,
        });

        WarmUp(options);

        var instances = new List<InstanceResult>();
        var runs = new List<RunResult>();
        var curves = new List<ConvergencePoint>();

        int index = 0;
        foreach (int pointCount in options.PointCounts)
        {
            for (int replica = 0; replica < options.InstancesPerSize; replica++, index++)
            {
                var instance = await BuildInstanceAsync(index, pointCount, options, http, exactSolver);
                instances.Add(instance);
                runs.AddRange(SolveInstance(instance, options, curves));

                Console.WriteLine($"  instância {index + 1,3}/{options.TotalInstances}  n={pointCount,2}  " +
                                  $"ótimo={Format(instance.OptimalDistanceM)}  " +
                                  $"melhor={instance.BestFoundM / 1000.0:0.000} km");
            }
        }

        WriteInstancesCsv(options, instances);
        WriteRunsCsv(options, runs);
        WriteConvergenceCsv(options, curves);
        var summary = Summarize(instances, runs);
        WriteSummaryCsv(options, summary);
        WriteMetadata(options, instances, runs);
        PrintSummary(summary);

        Console.WriteLine();
        Console.WriteLine($"Arquivos gravados em {Path.GetFullPath(options.OutputDirectory)}");
        return 0;
    }

    /// <summary>
    /// Runs every engine once on a throwaway instance. Without this the first measured run of each
    /// algorithm also pays for JIT compilation, which showed up as the first instance size looking
    /// slower than the larger ones.
    /// </summary>
    private static void WarmUp(BenchmarkOptions options)
    {
        var points = RandomPointGenerator.Generate(options.Region, 8, seed: 1);
        var matrix = HaversineDistance.Matrix(points);

        new NearestNeighborEngine().Solve(matrix, FixedStart);
        new TwoOptEngine().Solve(matrix, FixedStart);
        new ACOEngine(options.AcoParameters, seed: 1).SolveWithMetrics(matrix, FixedStart);
        new GeneticAlgorithmEngine(options.GaParameters, seed: 1).Solve(matrix, FixedStart);
        new ExactSolver(new ExactSolverLimits()).Solve(matrix, FixedStart);
    }

    // ---------------------------------------------------------------- instância

    private static async Task<InstanceResult> BuildInstanceAsync(
        int index, int pointCount, BenchmarkOptions options, HttpClient http, ExactSolver exactSolver)
    {
        var matrixService = new DistanceMatrixService(http);

        for (int attempt = 0; attempt < options.MaxGenerationAttempts; attempt++)
        {
            int seed = SeedDerivation.ForInstance(options.MasterSeed, index, attempt);
            var points = RandomPointGenerator.Generate(options.Region, pointCount, seed);

            double[,] matrix;
            try
            {
                matrix = options.UseOsrm
                    ? await matrixService.GetDistanceMatrixAsync(points)
                    : HaversineDistance.Matrix(points);
            }
            catch (UnreachableLocationException)
            {
                // Points that fall off the road network: redraw with the next attempt seed.
                continue;
            }
            finally
            {
                if (options.UseOsrm)
                    await Task.Delay(options.OsrmDelayMs);
            }

            RoundToMillimetres(matrix);

            double? optimum = null;
            ExactMethod? method = null;
            double exactMs = 0;
            if (exactSolver.Estimate(pointCount, fixedStart: true).IsFeasible)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.ExactTimeoutSeconds));
                var watch = Stopwatch.StartNew();
                try
                {
                    var solution = exactSolver.Solve(matrix, fixedStartCity: 0, cts.Token);
                    optimum = solution.Distance;
                    method = solution.Method;
                }
                catch (OperationCanceledException)
                {
                    // Keeps the instance: the reference becomes the best distance found by the heuristics.
                }
                exactMs = watch.Elapsed.TotalMilliseconds;
            }

            return new InstanceResult(index, pointCount, seed, points, matrix, optimum, method, exactMs);
        }

        throw new InvalidOperationException(
            $"Não foi possível gerar a instância {index} ({pointCount} pontos) em {options.MaxGenerationAttempts} tentativas.");
    }

    private static void RoundToMillimetres(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                matrix[i, j] = Math.Round(matrix[i, j], 3);
    }

    // ---------------------------------------------------------------- execução

    private static List<RunResult> SolveInstance(
        InstanceResult instance, BenchmarkOptions options, List<ConvergencePoint> curves)
    {
        var results = new List<RunResult>();

        results.Add(Measure(instance, "NearestNeighbor", 0,
            () => new NearestNeighborEngine().Solve(instance.Matrix, FixedStart)));

        results.Add(Measure(instance, "TwoOpt", 0,
            () => new TwoOptEngine().Solve(instance.Matrix, FixedStart)));

        for (int repetition = 0; repetition < options.Repetitions; repetition++)
        {
            int acoSeed = SeedDerivation.ForRun(instance.Seed, "aco-default", repetition);
            var engine = new ACOEngine(options.AcoParameters, acoSeed);

            var watch = Stopwatch.StartNew();
            var metrics = engine.SolveWithMetrics(instance.Matrix, FixedStart);
            watch.Stop();

            results.Add(new RunResult(instance, "ACO", repetition, metrics.BestDistance,
                watch.Elapsed.TotalMilliseconds, acoSeed, metrics.BestIteration, metrics.BestTour));

            if (repetition == 0)
            {
                for (int iteration = 0; iteration < metrics.ConvergenceCurve.Length; iteration++)
                    curves.Add(new ConvergencePoint(instance.Index, instance.PointCount, repetition,
                        iteration + 1, metrics.ConvergenceCurve[iteration]));
            }
        }

        for (int repetition = 0; repetition < options.Repetitions; repetition++)
        {
            int gaSeed = SeedDerivation.ForRun(instance.Seed, "ga-default", repetition);
            results.Add(Measure(instance, "GeneticAlgorithm", repetition,
                () => new GeneticAlgorithmEngine(options.GaParameters, gaSeed).Solve(instance.Matrix, FixedStart),
                gaSeed));
        }

        instance.BestFoundM = results.Min(r => r.DistanceM);
        return results;
    }

    /// <summary>All engines start from point 0, as in the dataset module.</summary>
    private const int FixedStart = 0;

    private static RunResult Measure(
        InstanceResult instance, string algorithm, int repetition,
        Func<(List<int> BestTour, double BestDistance)> solve, int? seed = null)
    {
        var watch = Stopwatch.StartNew();
        var (tour, distance) = solve();
        watch.Stop();
        return new RunResult(instance, algorithm, repetition, distance,
            watch.Elapsed.TotalMilliseconds, seed, null, tour);
    }

    // ---------------------------------------------------------------- agregação

    private static List<SummaryRow> Summarize(List<InstanceResult> instances, List<RunResult> runs)
    {
        var rows = new List<SummaryRow>();

        foreach (var group in runs.GroupBy(r => (r.Instance.PointCount, r.Algorithm))
                                  .OrderBy(g => g.Key.PointCount)
                                  .ThenBy(g => AlgorithmOrder(g.Key.Algorithm)))
        {
            var perInstance = group.GroupBy(r => r.Instance)
                .Select(g => new
                {
                    Instance = g.Key,
                    MeanDistance = g.Average(r => r.DistanceM),
                    MeanTime = g.Average(r => r.TimeMs),
                    StdDistance = StandardDeviation(g.Select(r => r.DistanceM).ToList()),
                })
                .ToList();

            var gaps = new List<double>();
            foreach (var item in perInstance)
            {
                double reference = item.Instance.ReferenceM;
                if (reference > 0)
                    gaps.Add((item.MeanDistance - reference) / reference * 100.0);
            }

            rows.Add(new SummaryRow(
                group.Key.PointCount,
                group.Key.Algorithm,
                perInstance.Count,
                perInstance.Average(i => i.MeanDistance) / 1000.0,
                gaps.Count > 0 ? gaps.Average() : double.NaN,
                perInstance.Average(i => i.MeanTime),
                perInstance.Average(i => i.StdDistance) / 1000.0,
                instances.Where(i => i.PointCount == group.Key.PointCount).All(i => i.OptimalDistanceM.HasValue)));
        }

        return rows;
    }

    private static int AlgorithmOrder(string algorithm) => algorithm switch
    {
        "ACO" => 0,
        "NearestNeighbor" => 1,
        "TwoOpt" => 2,
        "GeneticAlgorithm" => 3,
        _ => 4,
    };

    private static double StandardDeviation(List<double> values)
    {
        if (values.Count < 2) return 0;
        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    // ---------------------------------------------------------------- saída

    private static void WriteInstancesCsv(BenchmarkOptions options, List<InstanceResult> instances)
    {
        var sb = new StringBuilder("instance_index,point_count,instance_seed,optimal_distance_m,exact_method,exact_time_ms,reference_m,reference_kind,coordinates\n");
        foreach (var i in instances)
        {
            string coordinates = JsonSerializer.Serialize(i.Points.Select(p => new[] { p.Lat, p.Lng }));
            sb.Append(CultureInfo.InvariantCulture,
                $"{i.Index},{i.PointCount},{i.Seed},{Csv(i.OptimalDistanceM)},{i.ExactMethod},{i.ExactTimeMs:0.###},{i.ReferenceM:0.###},{i.ReferenceKind},\"{coordinates.Replace("\"", "\"\"")}\"\n");
        }
        File.WriteAllText(Path.Combine(options.OutputDirectory, "instances.csv"), sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteRunsCsv(BenchmarkOptions options, List<RunResult> runs)
    {
        var sb = new StringBuilder("instance_index,point_count,algorithm,repetition,seed,distance_m,time_ms,best_iteration,optimal_distance_m,reference_m,reference_kind,gap_percent,tour\n");
        foreach (var r in runs)
        {
            double gap = r.Instance.ReferenceM > 0
                ? (r.DistanceM - r.Instance.ReferenceM) / r.Instance.ReferenceM * 100.0
                : double.NaN;
            sb.Append(CultureInfo.InvariantCulture,
                $"{r.Instance.Index},{r.Instance.PointCount},{r.Algorithm},{r.Repetition},{Csv(r.Seed)},{r.DistanceM:0.###},{r.TimeMs:0.###},{Csv(r.BestIteration)},{Csv(r.Instance.OptimalDistanceM)},{r.Instance.ReferenceM:0.###},{r.Instance.ReferenceKind},{gap:0.######},\"{string.Join(' ', r.Tour)}\"\n");
        }
        File.WriteAllText(Path.Combine(options.OutputDirectory, "runs.csv"), sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteConvergenceCsv(BenchmarkOptions options, List<ConvergencePoint> curves)
    {
        var sb = new StringBuilder("instance_index,point_count,repetition,iteration,best_distance_m\n");
        foreach (var c in curves)
            sb.Append(CultureInfo.InvariantCulture,
                $"{c.InstanceIndex},{c.PointCount},{c.Repetition},{c.Iteration},{c.BestDistanceM:0.###}\n");
        File.WriteAllText(Path.Combine(options.OutputDirectory, "convergence.csv"), sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteSummaryCsv(BenchmarkOptions options, List<SummaryRow> summary)
    {
        var sb = new StringBuilder("point_count,algorithm,instances,mean_distance_km,mean_gap_percent,mean_time_ms,mean_std_km,reference_is_optimum\n");
        foreach (var s in summary)
            sb.Append(CultureInfo.InvariantCulture,
                $"{s.PointCount},{s.Algorithm},{s.Instances},{s.MeanDistanceKm:0.###},{s.MeanGapPercent:0.###},{s.MeanTimeMs:0.###},{s.MeanStdKm:0.###},{s.ReferenceIsOptimum}\n");
        File.WriteAllText(Path.Combine(options.OutputDirectory, "summary.csv"), sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteMetadata(BenchmarkOptions options, List<InstanceResult> instances, List<RunResult> runs)
    {
        var metadata = new
        {
            experiment = "Comparação entre algoritmos (ACO, Vizinho Mais Próximo, 2-Opt e Algoritmo Genético)",
            generatedAtUtc = DateTime.UtcNow,
            region = new { options.Region.South, options.Region.West, options.Region.North, options.Region.East },
            regionAreaKm2 = Math.Round(options.Region.AreaKm2, 1),
            options.MasterSeed,
            pointCounts = options.PointCounts,
            options.InstancesPerSize,
            options.Repetitions,
            distanceSource = options.UseOsrm ? "Osrm" : "Haversine",
            objective = "open_path",
            fixedStartIndex = FixedStart,
            aco = options.AcoParameters,
            ga = options.GaParameters,
            instances = instances.Count,
            runs = runs.Count,
            exactLimits = new { options.BruteForceMaxPoints, options.MaxExactPoints, options.ExactTimeoutSeconds },
        };
        File.WriteAllText(Path.Combine(options.OutputDirectory, "metadata.json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
    }

    private static void PrintHeader(BenchmarkOptions options)
    {
        Console.WriteLine("Experimento de comparação entre algoritmos");
        Console.WriteLine($"  região            : {options.Region} ({options.Region.AreaKm2:0.0} km²)");
        Console.WriteLine($"  tamanhos          : {string.Join(", ", options.PointCounts)}");
        Console.WriteLine($"  instâncias/tamanho: {options.InstancesPerSize}  (total {options.TotalInstances})");
        Console.WriteLine($"  repetições        : {options.Repetitions} (ACO e AG; NN e 2-Opt são determinísticos)");
        Console.WriteLine($"  distâncias        : {(options.UseOsrm ? "OSRM" : "Haversine")}");
        Console.WriteLine($"  semente mestra    : {options.MasterSeed}");
        Console.WriteLine();
    }

    private static void PrintSummary(List<SummaryRow> summary)
    {
        Console.WriteLine();
        Console.WriteLine("=== Resumo (Tabela 1 do artigo)");
        Console.WriteLine($"{"n",3}  {"Algoritmo",-18} {"Dist. (km)",11} {"Gap (%)",9} {"Tempo (ms)",11} {"Desvio (km)",12}");
        foreach (var s in summary)
            Console.WriteLine($"{s.PointCount,3}  {s.Algorithm,-18} {s.MeanDistanceKm,11:0.000} {s.MeanGapPercent,9:0.00} {s.MeanTimeMs,11:0.00} {s.MeanStdKm,12:0.000}");
        Console.WriteLine();
        Console.WriteLine("Gap relativo ao ótimo exato quando disponível; caso contrário, à melhor distância encontrada na instância.");
    }

    private static HttpClient CreateOsrmClient()
    {
        var client = new HttpClient { BaseAddress = new Uri("https://router.project-osrm.org/") };
        // The public OSRM demo server rejects requests without a User-Agent (HTTP 403).
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AntOptimization/1.0 (TCC route optimization)");
        client.Timeout = TimeSpan.FromMinutes(2);
        return client;
    }

    private static string Csv(double? value) =>
        value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "";

    private static string Csv(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "";

    private static string Format(double? value) =>
        value.HasValue ? $"{value.Value / 1000.0:0.000} km" : "   —    ";
}

// -------------------------------------------------------------------- modelos

internal sealed class InstanceResult(
    int index, int pointCount, int seed, List<Location> points, double[,] matrix,
    double? optimalDistanceM, ExactMethod? exactMethod, double exactTimeMs)
{
    public int Index { get; } = index;
    public int PointCount { get; } = pointCount;
    public int Seed { get; } = seed;
    public List<Location> Points { get; } = points;
    public double[,] Matrix { get; } = matrix;
    public double? OptimalDistanceM { get; } = optimalDistanceM;
    public ExactMethod? ExactMethod { get; } = exactMethod;
    public double ExactTimeMs { get; } = exactTimeMs;

    /// <summary>Best distance found by any algorithm on this instance; filled after the runs.</summary>
    public double BestFoundM { get; set; }

    /// <summary>Reference used for the gap: the exact optimum when known, else the best found.</summary>
    public double ReferenceM => OptimalDistanceM ?? BestFoundM;

    public string ReferenceKind => OptimalDistanceM.HasValue ? "optimum" : "best_found";
}

internal sealed record RunResult(
    InstanceResult Instance, string Algorithm, int Repetition, double DistanceM,
    double TimeMs, int? Seed, int? BestIteration, List<int> Tour);

internal sealed record ConvergencePoint(
    int InstanceIndex, int PointCount, int Repetition, int Iteration, double BestDistanceM);

internal sealed record SummaryRow(
    int PointCount, string Algorithm, int Instances, double MeanDistanceKm,
    double MeanGapPercent, double MeanTimeMs, double MeanStdKm, bool ReferenceIsOptimum);
