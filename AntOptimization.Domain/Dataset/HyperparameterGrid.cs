using System.Globalization;
using AntOptimization.Domain.Algorithms;

namespace AntOptimization.Domain.Dataset;

/// <summary>Sets of values to test for each ACO hyperparameter.</summary>
public class HyperparameterGrid
{
    public List<double> Alpha { get; set; } = [0.5, 1.0, 2.0];
    public List<double> Beta { get; set; } = [1.0, 2.0, 5.0];
    public List<int> NumberOfAnts { get; set; } = [10, 20, 40];
    public List<double> EvaporationRate { get; set; } = [0.1, 0.3, 0.5, 0.7];
    public List<int> Iterations { get; set; } = [50, 100, 200];

    public int ConfigurationCount =>
        Distinct(Alpha).Count * Distinct(Beta).Count * Distinct(NumberOfAnts).Count *
        Distinct(EvaporationRate).Count * Distinct(Iterations).Count;

    /// <summary>Returns validation errors; empty when the grid is valid.</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Alpha.Count == 0 || Alpha.Any(v => v < 0 || double.IsNaN(v)))
            errors.Add("Alpha: informe ao menos um valor ≥ 0.");
        if (Beta.Count == 0 || Beta.Any(v => v < 0 || double.IsNaN(v)))
            errors.Add("Beta: informe ao menos um valor ≥ 0.");
        if (NumberOfAnts.Count == 0 || NumberOfAnts.Any(v => v < 1 || v > 1000))
            errors.Add("Número de formigas: valores entre 1 e 1000.");
        if (EvaporationRate.Count == 0 || EvaporationRate.Any(v => v <= 0 || v >= 1))
            errors.Add("Taxa de evaporação: valores no intervalo (0, 1).");
        if (Iterations.Count == 0 || Iterations.Any(v => v < 1 || v > 10000))
            errors.Add("Iterações: valores entre 1 e 10000.");
        return errors;
    }

    public IEnumerable<GridConfiguration> Expand()
    {
        foreach (double alpha in Distinct(Alpha))
            foreach (double beta in Distinct(Beta))
                foreach (int ants in Distinct(NumberOfAnts))
                    foreach (double rho in Distinct(EvaporationRate))
                        foreach (int iterations in Distinct(Iterations))
                        {
                            var parameters = new ACOParameters
                            {
                                Alpha = alpha,
                                Beta = beta,
                                NumberOfAnts = ants,
                                EvaporationRate = rho,
                                Iterations = iterations
                            };
                            yield return new GridConfiguration(ConfigKey(parameters), parameters);
                        }
    }

    public static string ConfigKey(ACOParameters p) => string.Create(CultureInfo.InvariantCulture,
        $"a{p.Alpha:0.###}_b{p.Beta:0.###}_m{p.NumberOfAnts}_r{p.EvaporationRate:0.###}_i{p.Iterations}");

    private static List<T> Distinct<T>(List<T> values) => values.Distinct().OrderBy(v => v).ToList();
}

public record GridConfiguration(string Key, ACOParameters Parameters);
