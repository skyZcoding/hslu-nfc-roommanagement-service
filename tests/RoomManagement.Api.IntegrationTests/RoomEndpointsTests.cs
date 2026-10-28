using System.Net;
using System.Net.Http.Json;
using System.Text;
using RoomManagement.Application.Rooms;

namespace RoomManagement.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RoomEndpointsTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string RoomsUrl = $"{ApiRoot}/rooms";

    private Task<HttpResponseMessage> PostRoomAsync(object body) => Client.PostAsJsonAsync(RoomsUrl, body, Json);

    private Task<HttpResponseMessage> PutReaderAsync(Guid roomId, string readerId) =>
        Client.PutAsJsonAsync($"{RoomsUrl}/{roomId}/reader", new { readerId }, Json);

    private async Task<RoomDto> GetRoomAsync(Guid id) => await ReadAsync<RoomDto>(await Client.GetAsync($"{RoomsUrl}/{id}"));

    // ---- create ----

    [Fact]
    public async Task Create_ReturnsCreatedWithLocation_AndTheRoomCanBeFetched()
    {
        var response = await PostRoomAsync(new { name = " H2.301 ", building = "Campus Rotkreuz", floor = 3, capacity = 24 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<RoomDto>(response);
        Assert.Equal("H2.301", created.Name);
        Assert.Equal("Campus Rotkreuz", created.Building);
        Assert.Equal(3, created.Floor);
        Assert.Equal(24, created.Capacity);
        Assert.Null(created.ReaderId);
        Assert.True(created.IsActive);
        Assert.EndsWith($"{RoomsUrl}/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal(created, await ReadAsync<RoomDto>(await Client.GetAsync(response.Headers.Location)));
    }

    [Fact]
    public async Task Create_InvalidBody_ReturnsValidationProblemWithCamelCaseFieldNames()
    {
        var response = await PostRoomAsync(new { name = "", building = " ", floor = 1, capacity = 0 });

        var problem = await AssertValidationProblemAsync(response);

        Assert.Contains("name", problem.Errors.Keys);
        Assert.Contains("building", problem.Errors.Keys);
        Assert.Contains("capacity", problem.Errors.Keys);
        Assert.DoesNotContain("floor", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_ReturnsConflict()
    {
        await CreateRoomAsync("H2.301");

        var response = await PostRoomAsync(new { name = "h2.301", building = "Elsewhere", floor = 1, capacity = 5 });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "room_name_taken");
    }

    [Fact]
    public async Task Create_WithoutFloor_IsRejected_InsteadOfDefaultingToTheGroundFloor()
    {
        var response = await Client.PostAsync(RoomsUrl, new StringContent(
            """{ "name": "H2.301", "building": "Campus Rotkreuz", "capacity": 24 }""", Encoding.UTF8, "application/json"));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "bad_request");
        Assert.Contains("floor", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // ---- get ----

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{RoomsUrl}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "room_not_found");
    }

    [Fact]
    public async Task Get_IdThatIsNotAGuid_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{RoomsUrl}/not-a-guid");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    // ---- list ----

    [Fact]
    public async Task List_FiltersByBuildingIgnoringCaseAndActive_OrderedByBuildingFloorName()
    {
        await CreateRoomAsync("L1", "Campus Luzern", 1);
        await CreateRoomAsync("B2", "Campus Rotkreuz", 2);
        await CreateRoomAsync("A1", "Campus Rotkreuz", 1);
        var inactive = await CreateRoomAsync("Z9", "Campus Rotkreuz", 1);
        await DeactivateAsync("rooms", inactive.Id);

        async Task<string[]> Names(string query) =>
            (await ReadAsync<List<RoomDto>>(await Client.GetAsync($"{RoomsUrl}{query}"))).Select(r => r.Name).ToArray();

        Assert.Equal(["L1", "A1", "Z9", "B2"], await Names(""));
        Assert.Equal(["L1", "A1", "B2"], await Names("?activeOnly=true"));
        Assert.Equal(["A1", "Z9", "B2"], await Names("?building=campus%20rotkreuz"));
        Assert.Equal(["A1", "B2"], await Names("?building=CAMPUS%20ROTKREUZ&activeOnly=true"));
        Assert.Empty(await Names("?building=Nowhere"));
    }

    [Fact]
    public async Task List_WithoutRooms_ReturnsAnEmptyArray()
    {
        var response = await Client.GetAsync(RoomsUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    // ---- update ----

    [Fact]
    public async Task Update_ReplacesDetails_AndKeepsTheReader()
    {
        var room = await CreateRoomAsync("H2.301");
        await AssignReaderAsync(room.Id, "reader-1");

        var response = await Client.PutAsJsonAsync(
            $"{RoomsUrl}/{room.Id}", new { name = "H2.302", building = "Main", floor = 4, capacity = 30 }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<RoomDto>(response);
        Assert.Equal("H2.302", updated.Name);
        Assert.Equal("Main", updated.Building);
        Assert.Equal(4, updated.Floor);
        Assert.Equal(30, updated.Capacity);
        Assert.Equal("reader-1", updated.ReaderId);
        Assert.Equal(updated, await GetRoomAsync(room.Id));
    }

    [Fact]
    public async Task Update_KeepingOwnNameInDifferentCase_Succeeds()
    {
        var room = await CreateRoomAsync("H2.301");

        var response = await Client.PutAsJsonAsync(
            $"{RoomsUrl}/{room.Id}", new { name = "h2.301", building = "Campus Rotkreuz", floor = 3, capacity = 24 }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_NameOfAnotherRoom_ReturnsConflict()
    {
        await CreateRoomAsync("A1");
        var room = await CreateRoomAsync("A2");

        var response = await Client.PutAsJsonAsync(
            $"{RoomsUrl}/{room.Id}", new { name = "a1", building = "Campus Rotkreuz", floor = 3, capacity = 24 }, Json);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "room_name_taken");
        Assert.Equal("A2", (await GetRoomAsync(room.Id)).Name);
    }

    [Fact]
    public async Task Update_InvalidBody_ReturnsValidationProblem()
    {
        var room = await CreateRoomAsync();

        var response = await Client.PutAsJsonAsync(
            $"{RoomsUrl}/{room.Id}", new { name = "H2.301", building = "Campus", floor = 1, capacity = -1 }, Json);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Contains("capacity", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.PutAsJsonAsync(
            $"{RoomsUrl}/{Guid.NewGuid()}", new { name = "H2.301", building = "Campus", floor = 1, capacity = 5 }, Json);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "room_not_found");
    }

    // ---- deactivate ----

    [Fact]
    public async Task Deactivate_ReturnsNoContent_AndKeepsTheRoomAsInactive()
    {
        var room = await CreateRoomAsync();

        var response = await Client.DeleteAsync($"{RoomsUrl}/{room.Id}");
        var again = await Client.DeleteAsync($"{RoomsUrl}/{room.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.False((await GetRoomAsync(room.Id)).IsActive);
    }

    [Fact]
    public async Task Deactivate_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.DeleteAsync($"{RoomsUrl}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "room_not_found");
    }

    // ---- reader ----

    [Fact]
    public async Task AssignReader_StoresTheNormalizedReader_AndTheRoomCanBeFoundByIt()
    {
        var room = await CreateRoomAsync();

        var response = await PutReaderAsync(room.Id, " Reader-H2_301 ");
        var byReader = await Client.GetAsync($"{RoomsUrl}/by-reader/READER-H2_301");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("reader-h2_301", (await GetRoomAsync(room.Id)).ReaderId);
        Assert.Equal(HttpStatusCode.OK, byReader.StatusCode);
        Assert.Equal(room.Id, (await ReadAsync<RoomDto>(byReader)).Id);
    }

    [Fact]
    public async Task AssignReader_ReplacesTheCurrentReader()
    {
        var room = await CreateRoomAsync();
        await AssignReaderAsync(room.Id, "reader-old");

        await AssignReaderAsync(room.Id, "reader-new");

        Assert.Equal("reader-new", (await GetRoomAsync(room.Id)).ReaderId);
        await AssertProblemAsync(await Client.GetAsync($"{RoomsUrl}/by-reader/reader-old"), HttpStatusCode.NotFound, "reader_not_found");
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"{RoomsUrl}/by-reader/reader-new")).StatusCode);
    }

    [Fact]
    public async Task AssignReader_SameReaderAgain_Succeeds()
    {
        var room = await CreateRoomAsync();
        await AssignReaderAsync(room.Id, "reader-1");

        var response = await PutReaderAsync(room.Id, "READER-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AssignReader_ReaderOfAnotherRoom_ReturnsConflictNamingThatRoom()
    {
        var other = await CreateRoomAsync("A1");
        await AssignReaderAsync(other.Id, "reader-1");
        var room = await CreateRoomAsync("A2");

        var response = await PutReaderAsync(room.Id, "reader-1");

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict, "reader_in_use");
        Assert.Contains("A1", problem.Detail);
        Assert.Null((await GetRoomAsync(room.Id)).ReaderId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    public async Task AssignReader_InvalidReaderId_ReturnsValidationProblem(string readerId)
    {
        var room = await CreateRoomAsync();

        var response = await PutReaderAsync(room.Id, readerId);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Contains("readerId", problem.Errors.Keys);
    }

    [Fact]
    public async Task AssignReader_UnknownRoom_ReturnsNotFoundProblem()
    {
        var response = await PutReaderAsync(Guid.NewGuid(), "reader-1");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "room_not_found");
    }

    [Fact]
    public async Task RemoveReader_ClearsIt_AndIsIdempotent()
    {
        var room = await CreateRoomAsync();
        await AssignReaderAsync(room.Id, "reader-1");

        var response = await Client.DeleteAsync($"{RoomsUrl}/{room.Id}/reader");
        var again = await Client.DeleteAsync($"{RoomsUrl}/{room.Id}/reader");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Null((await GetRoomAsync(room.Id)).ReaderId);
        await AssertProblemAsync(await Client.GetAsync($"{RoomsUrl}/by-reader/reader-1"), HttpStatusCode.NotFound, "reader_not_found");
    }

    [Fact]
    public async Task RemoveReader_UnknownRoom_ReturnsNotFoundProblem()
    {
        var response = await Client.DeleteAsync($"{RoomsUrl}/{Guid.NewGuid()}/reader");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "room_not_found");
    }

    [Theory]
    [InlineData("reader-9")]
    [InlineData("not%20a%20valid%20id!")]
    public async Task GetByReader_UnknownOrMalformedReader_ReturnsNotFoundProblem(string readerId)
    {
        var response = await Client.GetAsync($"{RoomsUrl}/by-reader/{readerId}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "reader_not_found");
    }
}
