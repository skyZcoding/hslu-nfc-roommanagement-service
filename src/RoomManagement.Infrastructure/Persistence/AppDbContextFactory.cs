using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoomManagement.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> create the context; scaffolding a migration does not connect to the database.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=roommanagement;Username=roommanagement;Password=design-time-only";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(connectionString);

        return new AppDbContext(options.Options);
    }
}
