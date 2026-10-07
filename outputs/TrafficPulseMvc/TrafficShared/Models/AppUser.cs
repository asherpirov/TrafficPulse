namespace TrafficShared.Models;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    // שינוי הערך מבטל גם עוגיות התחברות ישנות.
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
}
