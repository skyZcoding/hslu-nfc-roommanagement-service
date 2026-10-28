using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RoomManagement.Api.IntegrationTests;

/// <summary>Every test gets its own freshly migrated database and its own in-process app instance.</summary>
public abstract class ApiTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected const string ApiRoot = "/api/v1";

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected PostgresFixture Postgres { get; } = postgres;
    protected string ConnectionString { get; private set; } = null!;
    protected ApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        ConnectionString = await Postgres.CreateDatabaseAsync();
        Factory = new ApiFactory(ConnectionString);
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<T>(Json);
        return body ?? throw new InvalidOperationException("The response body was empty.");
    }

    /// <summary>Asserts an RFC 9457 problem response and returns it.</summary>
    protected static async Task<ProblemDetails> AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ReadAsync<ProblemDetails>(response);
        Assert.Equal((int)status, problem.Status);
        if (code is not null)
            Assert.Equal(code, ((JsonElement)problem.Extensions["code"]!).GetString());

        return problem;
    }

    protected static async Task<HttpValidationProblemDetails> AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ReadAsync<HttpValidationProblemDetails>(response);
        Assert.Equal("validation_failed", ((JsonElement)problem.Extensions["code"]!).GetString());
        return problem;
    }
}
