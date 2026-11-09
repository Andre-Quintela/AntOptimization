using System.Globalization;
using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Dataset;

namespace AntOptimization.Benchmark;

/// <summary>Command-line options of the comparison experiment, with the protocol of section 3.7 as default.</summary>
internal sealed class BenchmarkOptions
{
    public const string Usage = """
        Uso: dotnet run -c Release --project AntOptimization.Benchmark -- [opções]

          --out <dir>          diretório de saída (padrão: datasets/comparacao)
          --sizes 10,15,30,50  tamanhos de instância
          --instances 10       instâncias por tamanho
          --reps 10            repetições dos algoritmos estocásticos (ACO e AG)
          --seed 20251109      semente mestra
          --haversine          usa distância de Haversine em vez do OSRM (sem rede)
          --osrm-delay 1000    intervalo entre requisições ao OSRM, em ms
        """;

    public string OutputDirectory { get; private set; } = Path.Combine("datasets", "comparacao");
    public List<int> PointCounts { get; private set; } = [10, 15, 30, 50];
    public int InstancesPerSize { get; private set; } = 10;
    public int Repetitions { get; private set; } = 10;
    public int MasterSeed { get; private set; } = 20251109;
    public bool UseOsrm { get; private set; } = true;
    public int OsrmDelayMs { get; private set; } = 1000;

    public GeoBounds Region { get; init; } = new(-20.39, -40.39, -20.24, -40.265);
    public int MaxGenerationAttempts => 3;
    public int BruteForceMaxPoints => 10;
    public int MaxExactPoints => 15;
    public int ExactTimeoutSeconds => 60;

    public ACOParameters AcoParameters { get; } = new();
    public GeneticAlgorithmParameters GaParameters { get; } = new();

    public int TotalInstances => PointCounts.Count * InstancesPerSize;

    public static BenchmarkOptions? Parse(string[] args)
    {
        var options = new BenchmarkOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string flag = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (flag)
            {
                case "--out":
                    if (Next() is not { } dir) return null;
                    options.OutputDirectory = dir;
                    break;
                case "--sizes":
                    if (Next() is not { } sizes) return null;
                    var parsed = sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => int.TryParse(s, CultureInfo.InvariantCulture, out int v) ? v : -1)
                        .ToList();
                    if (parsed.Count == 0 || parsed.Any(v => v < 2)) return null;
                    options.PointCounts = parsed.Distinct().Order().ToList();
                    break;
                case "--instances":
                    if (!TryReadPositive(Next(), out int instances)) return null;
                    options.InstancesPerSize = instances;
                    break;
                case "--reps":
                    if (!TryReadPositive(Next(), out int reps)) return null;
                    options.Repetitions = reps;
                    break;
                case "--seed":
                    if (Next() is not { } seedText || !int.TryParse(seedText, CultureInfo.InvariantCulture, out int seed)) return null;
                    options.MasterSeed = seed;
                    break;
                case "--haversine":
                    options.UseOsrm = false;
                    break;
                case "--osrm-delay":
                    if (Next() is not { } delayText || !int.TryParse(delayText, CultureInfo.InvariantCulture, out int delay) || delay < 0) return null;
                    options.OsrmDelayMs = delay;
                    break;
                case "--help" or "-h":
                    return null;
                default:
                    Console.WriteLine($"Opção desconhecida: {flag}");
                    return null;
            }
        }

        return options;
    }

    private static bool TryReadPositive(string? text, out int value) =>
        int.TryParse(text, CultureInfo.InvariantCulture, out value) && value > 0;
}
