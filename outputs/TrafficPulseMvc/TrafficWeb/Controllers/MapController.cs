using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficWeb.Services;

namespace TrafficWeb.Controllers;

[Authorize, EnableRateLimiting("map")]
public class MapController : Controller
{
    private readonly ITrafficRepository _repository;
    private readonly TomTomMapService _mapService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MapController> _logger;
    public MapController(ITrafficRepository repository, TomTomMapService mapService, IMemoryCache cache, ILogger<MapController> logger)
    {
        _repository = repository;
        _mapService = mapService;
        _cache = cache;
        _logger = logger;
    }
    [HttpGet]
    public async Task<IActionResult> Road(Guid id, CancellationToken cancellationToken)
    {
        var road = await _repository.GetRoadAsync(id);
        if (road == null || !road.IsActive) return NotFound();
        try
        {
            string cacheKey = "map:" + id;
            if (!_cache.TryGetValue(cacheKey, out byte[]? image))
            {
                image = await _mapService.GetMapAsync(road, cancellationToken);
                _cache.Set(cacheKey, image, TimeSpan.FromMinutes(1));
            }
            return File(image!, "image/svg+xml");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is BusinessException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Map unavailable: {Type}", ex.GetType().Name);
            // מוצגת הודעת כשל במקום מפה פיקטיבית. לא מדפיסים את המפתח.
            string fallback = "<svg xmlns='http://www.w3.org/2000/svg' width='1000' height='420'><rect width='1000' height='420' fill='#eef3f5'/><text x='500' y='210' text-anchor='middle' font-family='Arial' font-size='28' fill='#456070'>TomTom map unavailable - please try again later</text></svg>";
            return File(Encoding.UTF8.GetBytes(fallback), "image/svg+xml");
        }
    }
}
