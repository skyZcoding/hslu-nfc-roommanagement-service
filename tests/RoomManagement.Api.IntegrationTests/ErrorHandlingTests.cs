using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomManagement.Application.Common;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Common;
using RoomManagement.Domain.Users;

namespace RoomManagement.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ErrorHandlingTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private static readonly object ValidUser = new { email = "jan@example.com", displayName = "Jan", role = "Member" };

    public static TheoryData<Exception, HttpStatusCode, string> ExceptionsThatAreClientErrors => new()
    {
        { new DataConflictException("booking_overlap", "The room is already booked during this time."), HttpStatusCode.Conflict, "booking_overlap" },
        { new InvalidStateTransitionException("A Cancelled booking cannot be checked in."), HttpStatusCode.Conflict, "invalid_state" },
        { new DomainException("Capacity must be greater than zero."), HttpStatusCode.BadRequest, "invalid_request" },
    };

    private HttpClient ClientWith(Action<IServiceCollection> configureServices) =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(configureServices)).CreateClient();

    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync("/nope");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WrongHttpMethod_ReturnsMethodNotAllowedProblem()
    {
        var response = await Client.PatchAsJsonAsync($"{ApiRoot}/users", ValidUser, Json);

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task UnexpectedException_Returns500Problem_WithoutLeakingDetails()
    {
        var client = ClientWith(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddScoped<IUserRepository>(_ => new ThrowingUserRepository(
                new InvalidOperationException("secret connection details: Password=hunter2")));
        });

        var response = await client.GetAsync($"{ApiRoot}/users");

        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("at RoomManagement", body); // no stack trace
        Assert.NotNull(problem.Title);
    }

    [Theory]
    [MemberData(nameof(ExceptionsThatAreClientErrors))]
    public async Task ExceptionThrownWhileSaving_IsReportedWithItsStatusAndCode(
        Exception exception, HttpStatusCode expectedStatus, string expectedCode)
    {
        var client = ClientWith(services =>
        {
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(_ => new ThrowingUnitOfWork(exception));
        });

        var response = await client.PostAsJsonAsync($"{ApiRoot}/users", ValidUser, Json);

        var problem = await AssertProblemAsync(response, expectedStatus, expectedCode);
        Assert.Equal(exception.Message, problem.Detail);
    }

    private sealed class ThrowingUnitOfWork(Exception exception) : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.FromException(exception);
    }

    private sealed class ThrowingUserRepository(Exception exception) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw exception;

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => throw exception;

        public Task<IReadOnlyList<User>> ListAsync(bool activeOnly, UserRole? role, CancellationToken cancellationToken) =>
            throw exception;

        public void Add(User user) => throw exception;
    }
}
