using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficWeb.ViewModels;

namespace TrafficWeb.Controllers;

[EnableRateLimiting("account")]
public class AccountController : Controller
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher<AppUser> _hasher;
    private static readonly AppUser DummyUser = new();
    private readonly string _dummyHash;
    public AccountController(IUserRepository repository, IPasswordHasher<AppUser> hasher)
    {
        _repository = repository;
        _hasher = hasher;
        _dummyHash = DummyPasswordHash.Value;
    }
    // עלות בדיקה דומה גם לכתובת שאינה קיימת, בלי לחשב hash בכל בקשה.
    private static readonly Lazy<string> DummyPasswordHash = new(() => new PasswordHasher<AppUser>().HashPassword(DummyUser, "UnusedAccountPassword2026!"));

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        AppUser? user = await _repository.GetByEmailAsync(model.Email.Trim().ToLowerInvariant());
        PasswordVerificationResult result = _hasher.VerifyHashedPassword(user ?? DummyUser, user?.PasswordHash ?? _dummyHash, model.Password);
        if (user == null || !user.IsActive || user.LockedUntilUtc > DateTime.UtcNow || result == PasswordVerificationResult.Failed)
        {
            if (user != null && (user.LockedUntilUtc == null || user.LockedUntilUtc <= DateTime.UtcNow))
                await _repository.RecordLoginAsync(user.Id, false);
            ModelState.AddModelError("", "לא ניתן להתחבר. בדקו פרטים או נסו שוב מאוחר יותר.");
            return View(model);
        }
        await _repository.RecordLoginAsync(user.Id, true);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role), new("stamp", user.SecurityStamp)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });
        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl) : RedirectToAction("Index", "Traffic");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (!model.Password.Any(char.IsLetter) || !model.Password.Any(char.IsDigit) || model.Name.Any(char.IsControl))
        {
            ModelState.AddModelError("", "בחרו שם תקין וסיסמה עם אות ומספר.");
            return View(model);
        }
        var user = new AppUser { Name = model.Name.Trim(), Email = model.Email.Trim().ToLowerInvariant(), Role = "User" };
        if (user.Name.Length < 2) { ModelState.AddModelError("Name", "שם קצר מדי."); return View(model); }
        user.PasswordHash = _hasher.HashPassword(user, model.Password);
        try { await _repository.AddUserAsync(user); }
        catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); return View(model); }
        TempData["Success"] = "החשבון נוצר. כעת אפשר להתחבר.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Denied() { Response.StatusCode = 403; return View(); }
}
