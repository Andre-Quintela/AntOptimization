using AntOptimization.Domain.Models;

namespace AntOptimization.Domain.Dataset;

/// <summary>Great-circle (straight-line) distances in meters — same unit as OSRM.</summary>
public static class HaversineDistance
{
    private const double EarthRadiusMeters = 6_371_000;

    public static double Meters(double lat1, double lng1, double lat2, double lng2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLng = ToRadians(lng2 - lng1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static double[,] Matrix(IReadOnlyList<Location> locations)
    {
        int n = locations.Count;
        var matrix = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                double d = Meters(locations[i].Lat, locations[i].Lng, locations[j].Lat, locations[j].Lng);
                matrix[i, j] = d;
                matrix[j, i] = d;
            }
        return matrix;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
