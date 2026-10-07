using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrafficShared.Repositories;
using TrafficWeb.Services;
using TrafficWeb.ViewModels;

namespace TrafficWeb.Controllers;

[Authorize]
public class TrafficController : Controller
{
    private readonly ITrafficRepository _repository;
    public TrafficController(ITrafficRepository repository) { _repository = repository; }

    public async Task<IActionResult> Index(bool favoritesOnly = false)
    {
        var model = new DashboardViewModel
        {
            Roads = (await _repository.GetRoadsAsync()).Where(r => r.IsActive).ToList(),
            Readings = await _repository.GetLatestAsync(),
            Favorites = await _repository.GetFavoritesAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)),
            OpenAlerts = (await _repository.GetAlertsAsync(status: "Open")).Count,
            FavoritesOnly = favoritesOnly
        };
        if (favoritesOnly) model.Roads = model.Roads.Where(r => model.Favorites.Contains(r.Id)).ToList();
        return View(model);
    }

    public async Task<IActionResult> History(Guid id, DateTime? from, DateTime? to)
    {
        var road = await _repository.GetRoadAsync(id);
        if (road == null) return NotFound();
        var model = new HistoryViewModel { Road = road, From = from?.Date ?? Display.Today.AddDays(-7), To = to?.Date ?? Display.Today };
        if (!ModelState.IsValid || model.From > model.To || (model.To - model.From).TotalDays > 90 || model.To > Display.Today)
        {
            ModelState.AddModelError("", "בחרו טווח תקין של עד 90 יום, ללא תאריך עתידי.");
            return View(model);
        }
        model.Readings = await _repository.GetHistoryAsync(id, Display.StartOfDayUtc(model.From), Display.StartOfDayUtc(model.To.AddDays(1)).AddTicks(-1));
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Favorite(Guid id, bool enabled)
    {
        await _repository.SetFavoriteAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), id, enabled);
        return RedirectToAction(nameof(Index));
    }
}
