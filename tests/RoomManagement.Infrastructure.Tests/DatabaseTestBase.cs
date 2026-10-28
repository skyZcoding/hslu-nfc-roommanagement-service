using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Application;
using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Rooms;
using RoomManagement.Domain.Users;
using RoomManagement.Infrastructure.Persistence;

namespace RoomManagement.Infrastructure.Tests;

/// <summary>
/// Every test runs against its own freshly migrated database, wired up exactly like the real application.
/// A scope (<see cref="InScope(Func{IServiceProvider, Task})"/>) plays the role of one HTTP request.
/// </summary>
public abstract class DatabaseTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected static readonly CancellationToken Ct = CancellationToken.None;
    protected static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    protected static readonly DateTimeOffset End = Start.AddHours(1);

    private ServiceProvider? _provider;

    protected ServiceProvider Provider => _provider ?? throw new InvalidOperationException("Test is not initialized.");

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(); // the generic host provides this in the real application
        services.AddApplication();
        services.AddInfrastructure(await postgres.CreateDatabaseAsync());

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public Task DisposeAsync() => _provider?.DisposeAsync().AsTask() ?? Task.CompletedTask;

    protected async Task InScope(Func<IServiceProvider, Task> action)
    {
        await using var scope = Provider.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    protected async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>Runs <paramref name="call"/> on a handler resolved in its own scope.</summary>
    protected Task<TResult> Run<THandler, TResult>(Func<THandler, Task<TResult>> call) where THandler : notnull =>
        InScope(sp => call(sp.GetRequiredService<THandler>()));

    /// <summary>Arranges test data directly through the DbContext, bypassing the code under test.</summary>
    protected Task SeedAsync(params object[] entities) => InScope(async sp =>
    {
        var db = sp.GetRequiredService<AppDbContext>();
        db.AddRange(entities);
        await db.SaveChangesAsync(Ct);
    });

    protected static User NewUser(string email = "jan@example.com", string displayName = "Jan", UserRole role = UserRole.Member) =>
        User.Create(email, displayName, role, Start);

    protected static Room NewRoom(string name = "H2.301", string building = "Campus Rotkreuz", int floor = 3, string? reader = null)
    {
        var room = Room.Create(name, building, floor, 24);
        if (reader is not null) room.AssignReader(ReaderId.Create(reader));
        return room;
    }

    protected static Booking NewBooking(Room room, User user, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        Booking.Create(room.Id, user.Id, start ?? Start, end ?? End);
}
