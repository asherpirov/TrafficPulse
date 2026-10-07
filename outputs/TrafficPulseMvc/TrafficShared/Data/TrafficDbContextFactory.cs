using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrafficShared.Data;

// משמש רק את dotnet ef בעת יצירת Migrations, לא את בקשות האתר.
public class TrafficDbContextFactory : IDesignTimeDbContextFactory<TrafficDbContext>
{
    public TrafficDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__MySql")
            ?? throw new InvalidOperationException("Set ConnectionStrings__MySql for EF tooling.");
        string version = Environment.GetEnvironmentVariable("Database__ServerVersion")
            ?? throw new InvalidOperationException("Set Database__ServerVersion, matching your MySQL server.");
        var options = new DbContextOptionsBuilder<TrafficDbContext>();
        options.UseMySql(connection, new MySqlServerVersion(Version.Parse(version)));
        return new TrafficDbContext(options.Options);
    }
}
