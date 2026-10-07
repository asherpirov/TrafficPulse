using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficWeb.ViewModels;

namespace TrafficWeb.Controllers;

// ההרשאה נבדקת בשרת גם בפנייה ישירה לכתובת.
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ITrafficRepository _traffic;
    private readonly IUserRepository _users;
    public AdminController(ITrafficRepository traffic, IUserRepository users) { _traffic = traffic; _users = users; }
    public async Task<IActionResult> Roads() => View(await _traffic.GetRoadsAsync());
    public async Task<IActionResult> Users() => View(await _users.GetUsersAsync());
    [HttpGet]
    public async Task<IActionResult> EditRoad(Guid? id)
    {
        if (id == null) return View(new RoadEditViewModel());
        Road? road = await _traffic.GetRoadAsync(id.Value);
        if (road == null) return NotFound();
        return View(new RoadEditViewModel { Id = road.Id, Name = road.Name, Latitude = road.Latitude, Longitude = road.Longitude, IsActive = road.IsActive, SigmaThreshold = road.SigmaThreshold, Version = road.Version });
    }
    [HttpPost]
    public async Task<IActionResult> EditRoad(RoadEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await _traffic.SaveRoadAsync(new Road
            {
                Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id, Name = model.Name,
                Latitude = model.Latitude, Longitude = model.Longitude, SigmaThreshold = model.SigmaThreshold,
                IsActive = model.IsActive, Version = model.Version
            });
        }
        catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); return View(model); }
        TempData["Success"] = "המקטע נשמר.";
        return RedirectToAction(nameof(Roads));
    }
    [HttpPost]
    public async Task<IActionResult> SetUserActive(Guid id, bool active)
    {
        try { await _users.SetUserActiveAsync(id, active); TempData["Success"] = "מצב המשתמש עודכן."; }
        catch (BusinessException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Users));
    }
}
