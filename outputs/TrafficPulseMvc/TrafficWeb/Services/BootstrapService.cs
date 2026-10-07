using Microsoft.AspNetCore.Identity;
using TrafficShared.Models;
using TrafficShared.Repositories;

namespace TrafficWeb.Services;

public class BootstrapService
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, bool demo)
    {
        var traffic = services.GetRequiredService<ITrafficRepository>();
        var users = services.GetRequiredService<IUserRepository>();
        var hasher = services.GetRequiredService<IPasswordHasher<AppUser>>();
        await traffic.InitializeAsync();
        if ((await users.GetUsersAsync()).Count == 0)
        {
            string email = demo ? "admin@traffic.demo" : config["Bootstrap:Email"] ?? "";
            string password = demo ? "TrafficDemo2026!" : config["Bootstrap:Password"] ?? "";
            if (password.Length < 12 || password.Length > 128 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit) ||
                !System.Net.Mail.MailAddress.TryCreate(email, out _))
                throw new InvalidOperationException("Set Bootstrap__Email and a strong Bootstrap__Password before first start.");
            var admin = new AppUser { Name = "מנהל המערכת", Email = email.Trim().ToLowerInvariant(), Role = "Admin" };
            admin.PasswordHash = hasher.HashPassword(admin, password);
            await users.AddUserAsync(admin);
            if (demo)
            {
                var viewer = new AppUser { Name = "משתמש הדגמה", Email = "user@traffic.demo" };
                viewer.PasswordHash = hasher.HashPassword(viewer, password);
                await users.AddUserAsync(viewer);
            }
        }
        if (!demo || (await traffic.GetRoadsAsync()).Count != 0) return;

        string[] names = ["גישה למרכז העיר", "ציר הכניסה הצפוני", "הדרך לאזור התעשייה"];
        for (int index = 0; index < names.Length; index++)
        {
            var road = new Road { Name = names[index], Latitude = 32.08 + index * 0.01, Longitude = 34.83, Version = 0 };
            await traffic.SaveRoadAsync(road);
            // היסטוריה סינתטית ל-4 שבועות; היא מסומנת Demo בכל רשומה.
            DateTime now = DateTime.UtcNow;
            for (int week = 4; week >= 1; week--)
                for (int minute = -30; minute <= 30; minute += 15)
                    await traffic.SaveReadingAsync(new TrafficReading
                    {
                        RoadId = road.Id, CollectedAtUtc = now.AddDays(-7 * week).AddMinutes(minute + 1),
                        CurrentSpeed = 50 + index * 5 + minute / 15, FreeFlowSpeed = 70, Confidence = 0.95, Source = "Demo"
                    });
            await traffic.SaveReadingAsync(new TrafficReading { RoadId = road.Id, CurrentSpeed = index == 0 ? 18 : 55, FreeFlowSpeed = 70, Confidence = 0.95, Source = "Demo" });
        }
    }
}
