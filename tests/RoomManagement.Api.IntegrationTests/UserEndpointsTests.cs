using System.Net;
using System.Net.Http.Json;
using System.Text;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Users;

namespace RoomManagement.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class UserEndpointsTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string UsersUrl = $"{ApiRoot}/users";

    private Task<HttpResponseMessage> PostUserAsync(object body) => Client.PostAsJsonAsync(UsersUrl, body, Json);

    private async Task<UserDto> CreateUserAsync(string email = "jan@example.com", string displayName = "Jan", string role = "Member")
    {
        var response = await PostUserAsync(new { email, displayName, role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<UserDto>(response);
    }

    // ---- create ----

    [Fact]
    public async Task Create_ReturnsCreatedWithLocation_AndTheUserCanBeFetched()
    {
        var response = await PostUserAsync(new { email = "Jan.Walker@Example.COM", displayName = " Jan Walker ", role = "Admin" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<UserDto>(response);
        Assert.Equal("jan.walker@example.com", created.Email);
        Assert.Equal("Jan Walker", created.DisplayName);
        Assert.Equal(UserRole.Admin, created.Role);
        Assert.True(created.IsActive);
        Assert.EndsWith($"{UsersUrl}/{created.Id}", response.Headers.Location!.ToString());

        var fetched = await ReadAsync<UserDto>(await Client.GetAsync(response.Headers.Location));
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Create_WritesEnumsAsNamesAndPropertiesInCamelCase()
    {
        var response = await PostUserAsync(new { email = "jan@example.com", displayName = "Jan", role = "Member" });

        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"role\":\"Member\"", json);
        Assert.Contains("\"displayName\":\"Jan\"", json);
        Assert.Contains("\"isActive\":true", json);
    }

    [Fact]
    public async Task Create_InvalidBody_ReturnsValidationProblemWithCamelCaseFieldNames()
    {
        var response = await PostUserAsync(new { email = "not-an-email", displayName = "", role = "Member" });

        var problem = await AssertValidationProblemAsync(response);

        Assert.Contains("email", problem.Errors.Keys);
        Assert.Contains("displayName", problem.Errors.Keys);
        Assert.DoesNotContain("role", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_DuplicateEmailIgnoringCase_ReturnsConflict()
    {
        await CreateUserAsync("jan@example.com");

        var response = await PostUserAsync(new { email = "JAN@example.com", displayName = "Other", role = "Member" });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "user_email_taken");
    }

    [Theory]
    [InlineData("""{ "email": "jan@example.com", "displayName": "Jan", "role": "Boss" }""")] // unknown role name
    [InlineData("""{ "email": "jan@example.com", "displayName": "Jan", "role": 1 }""")] // numbers are not accepted
    [InlineData("""{ "email": "jan@example.com", "displayName": "Jan" }""")] // a property is missing
    [InlineData("""{ "email": "jan@example.com", "displayName": "Jan", "role": "Member" """)] // truncated JSON
    [InlineData("not json at all")]
    public async Task Create_UnreadableBody_ReturnsBadRequestProblem(string body)
    {
        var response = await Client.PostAsync(UsersUrl, new StringContent(body, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "bad_request");
    }

    [Fact]
    public async Task Create_WithoutBody_ReturnsBadRequestProblem()
    {
        var response = await Client.PostAsync(UsersUrl, content: null);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    [Fact]
    public async Task Create_ErrorMessagesForMissingProperties_NameTheProperty()
    {
        var response = await Client.PostAsync(
            UsersUrl, new StringContent("""{ "email": "jan@example.com", "displayName": "Jan" }""", Encoding.UTF8, "application/json"));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);

        Assert.Contains("role", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // ---- get ----

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{UsersUrl}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "user_not_found");
    }

    [Fact]
    public async Task Get_IdThatIsNotAGuid_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{UsersUrl}/not-a-guid");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    // ---- list ----

    [Fact]
    public async Task List_ReturnsUsersOrderedByDisplayName_AndAppliesFilters()
    {
        await CreateUserAsync("carl@example.com", "Carl", "Admin");
        await CreateUserAsync("anna@example.com", "Anna", "Member");
        var bob = await CreateUserAsync("bob@example.com", "Bob", "Admin");
        await Client.DeleteAsync($"{UsersUrl}/{bob.Id}");

        async Task<string[]> Names(string query) =>
            (await ReadAsync<List<UserDto>>(await Client.GetAsync($"{UsersUrl}{query}"))).Select(u => u.DisplayName).ToArray();

        Assert.Equal(["Anna", "Bob", "Carl"], await Names(""));
        Assert.Equal(["Anna", "Carl"], await Names("?activeOnly=true"));
        Assert.Equal(["Bob", "Carl"], await Names("?role=Admin"));
        Assert.Equal(["Carl"], await Names("?activeOnly=true&role=Admin"));
    }

    [Fact]
    public async Task List_WithoutUsers_ReturnsAnEmptyArray()
    {
        var response = await Client.GetAsync(UsersUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_UnknownRole_ReturnsBadRequestProblem()
    {
        var response = await Client.GetAsync($"{UsersUrl}?role=Boss");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    // ---- update ----

    [Fact]
    public async Task Update_ReplacesDisplayNameAndRole()
    {
        var user = await CreateUserAsync(role: "Member");

        var response = await Client.PutAsJsonAsync($"{UsersUrl}/{user.Id}", new { displayName = "Jan W.", role = "Admin" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<UserDto>(response);
        Assert.Equal("Jan W.", updated.DisplayName);
        Assert.Equal(UserRole.Admin, updated.Role);
        Assert.Equal(user.Email, updated.Email);
        Assert.Equal(updated, await ReadAsync<UserDto>(await Client.GetAsync($"{UsersUrl}/{user.Id}")));
    }

    [Fact]
    public async Task Update_InvalidBody_ReturnsValidationProblem()
    {
        var user = await CreateUserAsync();

        var response = await Client.PutAsJsonAsync($"{UsersUrl}/{user.Id}", new { displayName = " ", role = "Member" }, Json);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Contains("displayName", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_WithoutRole_IsRejected_InsteadOfSilentlyChangingTheRole()
    {
        var admin = await CreateUserAsync(role: "Admin");

        var response = await Client.PutAsJsonAsync($"{UsersUrl}/{admin.Id}", new { displayName = "Jan" }, Json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "bad_request");
        Assert.Equal(UserRole.Admin, (await ReadAsync<UserDto>(await Client.GetAsync($"{UsersUrl}/{admin.Id}"))).Role);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.PutAsJsonAsync($"{UsersUrl}/{Guid.NewGuid()}", new { displayName = "Jan", role = "Member" }, Json);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "user_not_found");
    }

    // ---- deactivate ----

    [Fact]
    public async Task Deactivate_ReturnsNoContent_AndKeepsTheUserAsInactive()
    {
        var user = await CreateUserAsync();

        var response = await Client.DeleteAsync($"{UsersUrl}/{user.Id}");
        var again = await Client.DeleteAsync($"{UsersUrl}/{user.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.False((await ReadAsync<UserDto>(await Client.GetAsync($"{UsersUrl}/{user.Id}"))).IsActive);
    }

    [Fact]
    public async Task Deactivate_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.DeleteAsync($"{UsersUrl}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "user_not_found");
    }
}
