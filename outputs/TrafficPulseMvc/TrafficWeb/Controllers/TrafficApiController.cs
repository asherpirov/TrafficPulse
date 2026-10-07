using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrafficShared.Repositories;
using TrafficShared.Services;

namespace TrafficWeb.Controllers;

[Authorize, ApiController, Route("api/traffic")]
public class TrafficApiController : ControllerBase
{
    private readonly ITrafficRepository _repository;
    public TrafficApiController(ITrafficRepository repository) { _repository = repository; }
    [HttpGet("roads")]
    public async Task<IActionResult> Roads() => Ok(await _repository.GetRoadsAsync());
    [HttpGet("latest")]
    public async Task<IActionResult> Latest() => Ok((await _repository.GetLatestAsync()).Select(r => new { reading = r, stale = r.CollectedAtUtc < DateTime.UtcNow.AddMinutes(5) }));
    [HttpGet("roads/{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, DateTimeOffset from, DateTimeOffset to, int limit = 500)
    {
        if (from > to || (to - from).TotalDays > 90 || to > DateTimeOffset.UtcNow || limit is < 1 or > 500)
            return BadRequest(new { error = "טווח לא תקין: עד 90 יום ועד 500 מדידות." });
        if (await _repository.GetRoadAsync(id) == null) return NotFound();
        return Ok(await _repository.GetHistoryAsync(id, from.UtcDateTime, to.UtcDateTime, limit));
    }
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(Guid? roadId, string? status)
    {
        if (status != null) TrafficValidator.ValidateStatus(status);
        return Ok(await _repository.GetAlertsAsync(roadId, status));
    }
}
