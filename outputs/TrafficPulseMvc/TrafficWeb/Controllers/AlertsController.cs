using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficShared.Services;
using TrafficWeb.ViewModels;

namespace TrafficWeb.Controllers;

[Authorize]
public class AlertsController : Controller
{
    private readonly ITrafficRepository _repository;
    public AlertsController(ITrafficRepository repository) { _repository = repository; }
    public async Task<IActionResult> Index(Guid? roadId, string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) status = null;
        if (status != null) TrafficValidator.ValidateStatus(status);
        return View(new AlertsViewModel { Roads = await _repository.GetRoadsAsync(), Alerts = await _repository.GetAlertsAsync(roadId, status), Status = status, RoadId = roadId });
    }
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, string status, int version)
    {
        try
        {
            await _repository.UpdateAlertAsync(id, status, version, Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
            TempData["Success"] = "מצב ההתרעה עודכן.";
        }
        catch (BusinessException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
