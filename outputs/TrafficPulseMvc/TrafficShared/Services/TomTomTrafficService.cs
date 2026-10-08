using System.Globalization;
using System.Net;
using System.Text.Json;
using TrafficShared.Models;

namespace TrafficShared.Services;

public class TomTomTrafficService : ITrafficSource
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    public TomTomTrafficService(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Set TOMTOM_API_KEY for TomTom.");
    }

    public async Task<TrafficReading> GetReadingAsync(Road road, CancellationToken cancellationToken)
    {
        string point = road.Latitude.ToString(CultureInfo.InvariantCulture) + "," + road.Longitude.ToString(CultureInfo.InvariantCulture);
        string url = "https://api.tomtom.com/traffic/services/4/flowSegmentData/absolute/10/json?unit=kmph&point=" + point + "&key=" + Uri.EscapeDataString(_apiKey);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
            if ((response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500) && attempt < 3)
            {
                double seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? attempt * 2;
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 30)), cancellationToken);
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new BusinessException($"Traffic provider returned HTTP {(int)response.StatusCode}.", 502);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            JsonElement data = json.RootElement.GetProperty("flowSegmentData");
            var reading = new TrafficReading
            {
                RoadId = road.Id, CollectedAtUtc = DateTime.UtcNow, Source = "TomTom",
                CurrentSpeed = data.GetProperty("currentSpeed").GetDouble(),
                FreeFlowSpeed = data.GetProperty("freeFlowSpeed").GetDouble(),
                Confidence = data.GetProperty("confidence").GetDouble(),
                RoadClosed = data.GetProperty("roadClosure").GetBoolean()
            };
            TrafficValidator.Validate(reading);
            return reading;
        }
        throw new BusinessException("Traffic provider unavailable.", 502);
    }
}
