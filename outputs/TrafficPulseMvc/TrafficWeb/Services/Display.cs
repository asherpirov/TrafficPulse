namespace TrafficWeb.Services;
public static class Display
{
    private static readonly TimeZoneInfo Israel = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    public static string LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Israel).ToString("dd/MM HH:mm");
    public static DateTime StartOfDayUtc(DateTime date) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), Israel);
    public static DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Israel).Date;
    public static string Status(string status) => status switch { "Open" => "פתוחה", "Acknowledged" => "בטיפול", "Resolved" => "נסגרה", _ => status };
}
