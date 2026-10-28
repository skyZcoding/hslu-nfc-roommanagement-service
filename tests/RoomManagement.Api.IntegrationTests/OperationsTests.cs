using System.Net;
using System.Text.Json;

namespace RoomManagement.Api.IntegrationTests;

/// <summary>Health checks, API documentation and the startup migration.</summary>
[Collection(ApiCollection.Name)]
public class OperationsTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    // ---- health ----

    [Fact]
    public async Task Live_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_ReturnsHealthy_WhileTheDatabaseIsReachable()
    {
        var response = await Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_ReturnsUnavailable_WhenTheDatabaseIsDown_ButLiveStaysHealthy()
    {
        using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2;Pooling=false");
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var live = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    // ---- API documentation ----

    [Fact]
    public async Task InDevelopment_TheOpenApiDocumentAndSwaggerUiAreServed()
    {
        var document = await Client.GetAsync("/openapi/v1.json");
        var ui = await Client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        var json = await document.Content.ReadAsStringAsync();
        Assert.Contains("Room Management API", json);
        Assert.Contains("/api/v1/users/{id}", json);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Equal("text/html", ui.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TheOpenApiDocument_ListsEveryEndpointOfTheConcept()
    {
        (string Method, string Path)[] expected =
        [
            ("get", "/users"), ("post", "/users"), ("get", "/users/{id}"), ("put", "/users/{id}"), ("delete", "/users/{id}"),
            ("get", "/users/{userId}/bookings"),
            ("get", "/rooms"), ("post", "/rooms"), ("get", "/rooms/{id}"), ("put", "/rooms/{id}"), ("delete", "/rooms/{id}"),
            ("get", "/rooms/by-reader/{readerId}"), ("put", "/rooms/{id}/reader"), ("delete", "/rooms/{id}/reader"),
            ("get", "/rooms/{roomId}/bookings"), ("post", "/rooms/{roomId}/bookings"), ("get", "/rooms/{roomId}/availability"),
            ("get", "/bookings/{id}"), ("post", "/bookings/{id}/cancel"), ("post", "/bookings/{id}/check-in"),
            ("post", "/readers/{readerId}/scans"),
        ];

        using var document = JsonDocument.Parse(await Client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        var missing = expected
            .Where(e => !(paths.TryGetProperty($"{ApiRoot}{e.Path}", out var item) && item.TryGetProperty(e.Method, out _)))
            .Select(e => $"{e.Method.ToUpperInvariant()} {ApiRoot}{e.Path}")
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public async Task InProduction_TheOpenApiDocumentAndSwaggerUiAreNotServed()
    {
        using var factory = new ApiFactory(ConnectionString, environment: "Production");
        using var client = factory.CreateClient();

        var document = await client.GetAsync("/openapi/v1.json");
        var ui = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ui.StatusCode);
    }

    // ---- migration on startup ----

    [Fact]
    public async Task MigrateOnStartup_CreatesTheSchemaInAnEmptyDatabase()
    {
        using var factory = new ApiFactory(await Postgres.CreateEmptyDatabaseAsync(), migrateOnStartup: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{ApiRoot}/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutTheFlag_NothingMigratesTheDatabase()
    {
        using var factory = new ApiFactory(await Postgres.CreateEmptyDatabaseAsync(), migrateOnStartup: false);
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var users = await client.GetAsync($"{ApiRoot}/users");

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode); // reachable, just empty
        await AssertProblemAsync(users, HttpStatusCode.InternalServerError);
    }
}
