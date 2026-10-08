using System.Globalization;
using System.Text;
using TrafficShared.Models;

namespace TrafficWeb.Services;

// מפת תמונה סביב נקודה אחת: GET פשוט, ללא SDK וללא חישובי GIS.
public class TomTomMapService
{
    private readonly HttpClient _client;
    private readonly string _apiKey;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public TomTomMapService(HttpClient client, string apiKey)
    {
        _client = client;
        _apiKey = apiKey;
    }

    public async Task<byte[]> GetMapAsync(Road road, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new BusinessException("מפתח TomTom למפה אינו מוגדר.", 503);
        if (Math.Abs(road.Latitude) > 85) throw new BusinessException("המפה תומכת בקווי רוחב בין 85- ל־85.");
        // שימו לב: במפות הסדר הוא אורך,רוחב; ב-Traffic API הסדר הפוך.
        string center = road.Longitude.ToString(CultureInfo.InvariantCulture) + "," + road.Latitude.ToString(CultureInfo.InvariantCulture);
        string url = "https://api.tomtom.com/map/1/staticimage?center=" + center +
            "&zoom=13&width=1000&height=420&format=png&layer=basic&style=main&key=" + Uri.EscapeDataString(_apiKey);
        using var response = await _client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new BusinessException($"Map provider returned HTTP {(int)response.StatusCode}.", 502);
        if (response.Content.Headers.ContentType?.MediaType != "image/png")
            throw new BusinessException("Map provider returned an unexpected format.", 502);

        byte[] image = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        string encoded = Convert.ToBase64String(image);
        // התמונה ממורכזת בנקודה: הסמן נמצא בדיוק במרכז, ללא נוסחת הקרנה.
        string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='1000' height='420' viewBox='0 0 1000 420'>" +
            "<image width='1000' height='420' href='data:image/png;base64," + encoded + "'/>" +
            "<circle cx='500' cy='210' r='18' fill='#ffffff' opacity='.9'/><circle cx='500' cy='210' r='10' fill='#007e80'/>" +
            "<text x='980' y='403' text-anchor='end' font-family='Arial' font-size='13' fill='#142d3c'>© TomTom</text></svg>";
        return Encoding.UTF8.GetBytes(svg);
    }
}
