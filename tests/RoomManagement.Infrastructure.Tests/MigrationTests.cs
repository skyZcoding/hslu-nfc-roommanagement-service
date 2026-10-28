using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoomManagement.Infrastructure.Persistence;

namespace RoomManagement.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task MigrateDatabase_OnAnEmptyDatabase_CreatesTheSchemaAndLogsNoWarningsOrErrors()
    {
        var logs = new List<LogEntry>();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(new ListLoggerProvider(logs)));
        services.AddInfrastructure(await postgres.CreateEmptyDatabaseAsync());
        await using var provider = services.BuildServiceProvider();

        await provider.MigrateDatabaseAsync();

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Empty(await db.Users.ToListAsync()); // the tables exist
        Assert.Empty(logs.Where(log => log.Level >= LogLevel.Warning).Select(log => log.ToString()));
    }

    [Fact]
    public async Task MigrateDatabase_RunTwice_ChangesNothing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(await postgres.CreateEmptyDatabaseAsync());
        await using var provider = services.BuildServiceProvider();

        await provider.MigrateDatabaseAsync();
        await provider.MigrateDatabaseAsync();

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
    }

    private sealed record LogEntry(LogLevel Level, string Category, string Message)
    {
        public override string ToString() => $"{Level} {Category}: {Message}";
    }

    private sealed class ListLoggerProvider(List<LogEntry> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, entries);

        public void Dispose()
        {
        }
    }

    private sealed class ListLogger(string category, List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (entries)
                entries.Add(new LogEntry(logLevel, category, formatter(state, exception)));
        }
    }
}
