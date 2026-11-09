namespace AntOptimization.Domain.Dataset;

/// <summary>Rectangular geographic region (degrees).</summary>
public record GeoBounds(double South, double West, double North, double East)
{
    public bool IsValid =>
        South >= -90 && North <= 90 && West >= -180 && East <= 180 &&
        North > South && East > West;

    public double WidthKm => HaversineDistance.Meters((South + North) / 2, West, (South + North) / 2, East) / 1000.0;
    public double HeightKm => HaversineDistance.Meters(South, West, North, West) / 1000.0;
    public double AreaKm2 => WidthKm * HeightKm;

    public bool Contains(double lat, double lng) =>
        lat >= South && lat <= North && lng >= West && lng <= East;
}
