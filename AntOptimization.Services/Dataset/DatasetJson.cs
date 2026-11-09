using System.Text.Json;
using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.DTOs;
using AntOptimization.Domain.Models;

namespace AntOptimization.Services.Dataset;

/// <summary>JSON (de)serialization of the array-like columns stored with dataset entities.</summary>
internal static class DatasetJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string SerializeGrid(HyperparameterGrid grid) => JsonSerializer.Serialize(grid, Options);
    public static HyperparameterGrid DeserializeGrid(string json) =>
        JsonSerializer.Deserialize<HyperparameterGrid>(json, Options) ?? new HyperparameterGrid();

    public static string SerializePointCounts(IEnumerable<int> pointCounts) => JsonSerializer.Serialize(pointCounts, Options);
    public static List<int> DeserializePointCounts(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<int>>(json, Options) ?? [];

    public static string SerializeTour(IEnumerable<int> tour) => JsonSerializer.Serialize(tour, Options);
    public static List<int> DeserializeTour(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<int>>(json, Options) ?? [];

    public static string SerializeLocations(IEnumerable<Location> locations) =>
        JsonSerializer.Serialize(locations.Select(l => new LocationDto { Lat = l.Lat, Lng = l.Lng }), Options);
    public static List<LocationDto> DeserializeLocations(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<LocationDto>>(json, Options) ?? [];

    public static string SerializeMatrix(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        var rows = new double[n][];
        for (int i = 0; i < n; i++)
        {
            rows[i] = new double[n];
            for (int j = 0; j < n; j++)
                rows[i][j] = Math.Round(matrix[i, j], 3);
        }
        return JsonSerializer.Serialize(rows, Options);
    }

    public static double[,] DeserializeMatrix(string json)
    {
        var rows = JsonSerializer.Deserialize<double[][]>(json, Options) ?? [];
        int n = rows.Length;
        var matrix = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                matrix[i, j] = rows[i][j];
        return matrix;
    }
}
