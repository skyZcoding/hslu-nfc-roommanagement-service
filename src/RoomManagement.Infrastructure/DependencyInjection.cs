using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Application.Bookings;
using RoomManagement.Application.Common;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Users;
using RoomManagement.Infrastructure.Persistence;
using RoomManagement.Infrastructure.Persistence.Repositories;

namespace RoomManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return services.AddInfrastructure(_ => connectionString);
    }

    /// <param name="connectionString">
    /// Called when the database is first used rather than while services are registered, so it sees the final
    /// configuration (for example the overrides a test host applies).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseAppDatabase(connectionString(serviceProvider)));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IClock, SystemClock>();

        // The API reports the app as ready only while the database is reachable.
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        return services;
    }

    /// <summary>Creates the database if needed and applies all pending migrations.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();

        // On a new database EF Core first reads the migrations history table, which does not exist yet, and then
        // creates it. That expected failure would show up as an error in the log of every first start, so it is
        // not logged. Errors of real queries are unaffected: they are thrown and logged by the caller.
        var migrationOptions = new DbContextOptionsBuilder<AppDbContext>(options)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.CommandError))
            .Options;

        await using var db = new AppDbContext(migrationOptions);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
