using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficShared.Data;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficShared.Services;
using TrafficWeb.Middlewares;
using TrafficWeb.Services;

var builder = WebApplication.CreateBuilder(args);
bool demo = builder.Configuration.GetValue<bool>("App:DemoMode");
if (demo && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Demo mode is allowed only in Development.");

string dataPath = Path.GetFullPath(builder.Configuration["App:DataPath"] ?? Path.Combine(builder.Environment.ContentRootPath, ".data"));
Directory.CreateDirectory(dataPath);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataPath, "keys")));
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSingleton(builder.Configuration.GetSection("Analysis").Get<AnalysisSettings>() ?? new AnalysisSettings());
builder.Services.AddSingleton<AnomalyService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });
builder.Services.AddSingleton(provider => new TomTomMapService(provider.GetRequiredService<HttpClient>(), Environment.GetEnvironmentVariable("TOMTOM_API_KEY") ?? ""));
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210000);

if (demo)
{
    builder.Services.AddSingleton(provider => new DemoRepository(Path.Combine(dataPath, "demo.json"), provider.GetRequiredService<AnomalyService>()));
    builder.Services.AddSingleton<ITrafficRepository>(provider => provider.GetRequiredService<DemoRepository>());
    builder.Services.AddSingleton<IUserRepository>(provider => provider.GetRequiredService<DemoRepository>());
    builder.Services.AddHostedService<DemoRefreshService>();
}
else
{
    string connectionString = builder.Configuration.GetConnectionString("MySql") ?? throw new InvalidOperationException("Set ConnectionStrings__MySql.");
    builder.Services.AddDbContext<TrafficDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
    builder.Services.AddScoped<ITrafficRepository, TrafficRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
    options.Cookie.Name = "TrafficPulse.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.SlidingExpiration = false;
    options.Events.OnValidatePrincipal = async context =>
    {
        // בודקים מול המאגר בכל בקשה: השבתת משתמש מבטלת גישה גם עם עוגייה קיימת.
        string? value = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        AppUser? user = Guid.TryParse(value, out Guid id) ? await repository.GetUserAsync(id) : null;
        if (user == null || !user.IsActive || user.SecurityStamp != context.Principal?.FindFirstValue("stamp") || user.Role != context.Principal.FindFirstValue(ClaimTypes.Role))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    };
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 403;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("map", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await BootstrapService.InitializeAsync(scope.ServiceProvider, app.Configuration, demo);
app.UseMiddleware<GlobalExceptionMiddleware>();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'none'; style-src 'self'; img-src 'self' data:; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (!context.Request.Path.StartsWithSegments("/css")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Traffic}/{action=Index}/{id?}");
app.Run();
