using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RoomManagement.Api.IntegrationTests;

/// <summary>Starts the real application in-process, pointed at the given database.</summary>
public sealed class ApiFactory(
    string connectionString,
    string environment = "Development",
    bool migrateOnStartup = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Database:MigrateOnStartup"] = migrateOnStartup.ToString(),
            }));
    }
}
