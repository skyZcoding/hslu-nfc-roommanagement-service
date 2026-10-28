using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.Application.Bookings;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Users;

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

    // ---- arranging data through the API; each helper checks that the call it relies on succeeded ----

    protected async Task<UserDto> CreateUserAsync(string email = "jan@example.com", string displayName = "Jan", string role = "Member")
    {
        var response = await Client.PostAsJsonAsync($"{ApiRoot}/users", new { email, displayName, role }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<UserDto>(response);
    }

    protected async Task<RoomDto> CreateRoomAsync(
        string name = "H2.301", string building = "Campus Rotkreuz", int floor = 3, int capacity = 24)
    {
        var response = await Client.PostAsJsonAsync($"{ApiRoot}/rooms", new { name, building, floor, capacity }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<RoomDto>(response);
    }

    protected async Task AssignReaderAsync(Guid roomId, string readerId)
    {
        var response = await Client.PutAsJsonAsync($"{ApiRoot}/rooms/{roomId}/reader", new { readerId }, Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    protected async Task<BookingDto> CreateBookingAsync(Guid roomId, Guid userId, DateTimeOffset start, DateTimeOffset end)
    {
        var response = await Client.PostAsJsonAsync($"{ApiRoot}/rooms/{roomId}/bookings", new { userId, start, end }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<BookingDto>(response);
    }

    protected async Task DeactivateAsync(string collection, Guid id)
    {
        var response = await Client.DeleteAsync($"{ApiRoot}/{collection}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    protected static string Iso(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));

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
