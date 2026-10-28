using Microsoft.EntityFrameworkCore;

namespace RoomManagement.Infrastructure.Persistence;

internal static class DatabaseOptions
{
    /// <summary>The one place that decides how the app talks to PostgreSQL; shared by the app and design-time tooling.</summary>
    public static DbContextOptionsBuilder UseAppDatabase(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
