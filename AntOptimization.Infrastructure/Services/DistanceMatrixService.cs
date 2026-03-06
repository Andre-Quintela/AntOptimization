using System.Net.Http.Json;
using System.Text.Json;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;

namespace AntOptimization.Infrastructure.Services;

public class DistanceMatrixService : IDistanceMatrixService
{
    private readonly HttpClient _httpClient;

    public DistanceMatrixService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<double[,]> GetDistanceMatrixAsync(List<Location> locations)
    {
        int n = locations.Count;

        var coordinates = string.Join(";", locations.Select(l =>
            $"{l.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)},{l.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

        var url = $"table/v1/driving/{coordinates}?annotations=distance";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var distances = json.GetProperty("distances");

        var matrix = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            var row = distances[i];
            for (int j = 0; j < n; j++)
            {
                matrix[i, j] = row[j].GetDouble();
            }
        }

        return matrix;
    }

    public async Task<List<Location>> GetRouteCoordinatesAsync(List<Location> orderedLocations)
    {
        var coordinates = string.Join(";", orderedLocations.Select(l =>
            $"{l.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)},{l.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

        var url = $"route/v1/driving/{coordinates}?overview=full&geometries=geojson";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var routeCoordinates = json
            .GetProperty("routes")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates");

        var result = new List<Location>();
        foreach (var coord in routeCoordinates.EnumerateArray())
        {
            result.Add(new Location
            {
                Lat = coord[1].GetDouble(),
                Lng = coord[0].GetDouble()
            });
        }

        return result;
    }
}
