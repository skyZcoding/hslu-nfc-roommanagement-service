using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RoomManagement.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace RoomManagement.TestSupport;

/// <summary>
/// One PostgreSQL container for a whole test run. The migrations are applied once to a template database;
/// every test then gets its own copy of it, which is far cheaper than migrating again.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateName = "template_migrated";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await ExecuteAsync($"CREATE DATABASE \"{TemplateName}\"");

        var services = new ServiceCollection().AddInfrastructure(ConnectionStringFor(TemplateName));
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateDatabaseAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a new, fully migrated database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = NewDatabaseName();
        await ExecuteAsync($"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateName}\"");
        return ConnectionStringFor(name);
    }

    /// <summary>Creates a new database without any tables and returns its connection string.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = NewDatabaseName();
        await ExecuteAsync($"CREATE DATABASE \"{name}\"");
        return ConnectionStringFor(name);
    }

    private static string NewDatabaseName() => $"test_{Guid.NewGuid():N}";

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database,
            Pooling = false, // no idle connections that would block using the template database
        }.ConnectionString;

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
