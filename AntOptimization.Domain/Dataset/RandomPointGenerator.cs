using AntOptimization.Domain.Models;

namespace AntOptimization.Domain.Dataset;

/// <summary>
/// Generates N uniformly distributed points inside a rectangular region.
/// Deterministic for a given seed, so every instance can be regenerated from its seed.
/// </summary>
public static class RandomPointGenerator
{
    private const int CoordinateDecimals = 6; // ~0.1 m

    public static List<Location> Generate(GeoBounds bounds, int count, int seed)
    {
        if (!bounds.IsValid)
            throw new ArgumentException("Região inválida.", nameof(bounds));
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        var random = new Random(seed);
        var points = new List<Location>(count);
        var seen = new HashSet<(double, double)>();

        while (points.Count < count)
        {
            double lat = Math.Round(bounds.South + random.NextDouble() * (bounds.North - bounds.South), CoordinateDecimals);
            double lng = Math.Round(bounds.West + random.NextDouble() * (bounds.East - bounds.West), CoordinateDecimals);

            if (seen.Add((lat, lng)))
                points.Add(new Location { Lat = lat, Lng = lng });
        }

        return points;
    }
}
