using System.Net;
using System.Net.Http.Json;
using System.Text;
using RoomManagement.Application.Bookings;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Scans;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Bookings;

namespace RoomManagement.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CardScanEndpointsTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string ReaderId = "reader-h2-301";

    /// <summary>A user and a room whose reader is <see cref="ReaderId"/>.</summary>
    private async Task<(UserDto User, RoomDto Room)> ArrangeAsync()
    {
        var user = await CreateUserAsync("jan@example.com", "Jan Walker");
        var room = await CreateRoomAsync();
        await AssignReaderAsync(room.Id, ReaderId);
        return (user, room);
    }

    private Task<HttpResponseMessage> ScanAsync(Guid userId, string readerId = ReaderId) =>
        Client.PostAsJsonAsync($"{ApiRoot}/readers/{readerId}/scans", new { userId }, Json);

    private async Task<CardScanResult> ScanResultAsync(Guid userId, string readerId = ReaderId)
    {
        var response = await ScanAsync(userId, readerId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<CardScanResult>(response);
    }

    private async Task<BookingDto> GetBookingAsync(Guid id) =>
        await ReadAsync<BookingDto>(await Client.GetAsync($"{ApiRoot}/bookings/{id}"));

    [Fact]
    public async Task UserWithABooking_IsCheckedInAndGranted()
    {
        var (user, room) = await ArrangeAsync();
        var now = DateTimeOffset.UtcNow;
        var booking = await CreateBookingAsync(room.Id, user.Id, now.AddMinutes(5), now.AddMinutes(65));

        var response = await ScanAsync(user.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"granted\":true", json);
        Assert.Contains("\"reason\":null", json);
        var scan = System.Text.Json.JsonSerializer.Deserialize<CardScanResult>(json, Json)!;
        Assert.Equal(room.Id, scan.RoomId);
        Assert.Equal(booking.Id, scan.BookingId);
        Assert.Equal("Jan Walker", scan.UserDisplayName);
        var stored = await GetBookingAsync(booking.Id);
        Assert.Equal(BookingStatus.CheckedIn, stored.Status);
        Assert.NotNull(stored.CheckedInAt);
    }

    [Fact]
    public async Task ScanningAgain_IsDeniedAsAlreadyCheckedIn()
    {
        var (user, room) = await ArrangeAsync();
        var now = DateTimeOffset.UtcNow;
        await CreateBookingAsync(room.Id, user.Id, now.AddMinutes(5), now.AddMinutes(65));
        await ScanAsync(user.Id);

        var response = await ScanAsync(user.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"granted\":false", json);
        Assert.Contains("\"reason\":\"AlreadyCheckedIn\"", json);
        Assert.Contains("\"bookingId\":null", json);
    }

    [Fact]
    public async Task UserWithoutABooking_IsDenied_AndNothingIsCreated()
    {
        var (user, room) = await ArrangeAsync();

        var scan = await ScanResultAsync(user.Id);

        Assert.False(scan.Granted);
        Assert.Equal(ScanDenialReason.NoActiveBooking, scan.Reason);
        Assert.Equal(room.Id, scan.RoomId);
        Assert.Equal("Jan Walker", scan.UserDisplayName);
        Assert.Equal("[]", await (await Client.GetAsync($"{ApiRoot}/rooms/{room.Id}/bookings")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BookingThatStartsLater_IsNotCheckedInYet()
    {
        var (user, room) = await ArrangeAsync();
        var now = DateTimeOffset.UtcNow;
        var booking = await CreateBookingAsync(room.Id, user.Id, now.AddHours(2), now.AddHours(3));

        var scan = await ScanResultAsync(user.Id);

        Assert.Equal(ScanDenialReason.NoActiveBooking, scan.Reason);
        Assert.Equal(BookingStatus.Booked, (await GetBookingAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task ReaderIdInTheUrl_IsMatchedIgnoringCase()
    {
        var (user, room) = await ArrangeAsync();
        var now = DateTimeOffset.UtcNow;
        await CreateBookingAsync(room.Id, user.Id, now.AddMinutes(5), now.AddMinutes(65));

        var scan = await ScanResultAsync(user.Id, "READER-H2-301");

        Assert.True(scan.Granted);
    }

    [Fact]
    public async Task UnknownUserAndEmptyUserId_AreDeniedAsUserUnknown()
    {
        await ArrangeAsync();

        var unknown = await ScanResultAsync(Guid.NewGuid());
        var empty = await ScanResultAsync(Guid.Empty);

        Assert.Equal(ScanDenialReason.UserUnknown, unknown.Reason);
        Assert.Equal(ScanDenialReason.UserUnknown, empty.Reason);
        Assert.Null(unknown.UserDisplayName);
    }

    [Fact]
    public async Task InactiveUser_IsDenied_EvenWithABooking()
    {
        var (user, room) = await ArrangeAsync();
        var now = DateTimeOffset.UtcNow;
        var booking = await CreateBookingAsync(room.Id, user.Id, now.AddMinutes(5), now.AddMinutes(65));
        await DeactivateAsync("users", user.Id);

        var scan = await ScanResultAsync(user.Id);

        Assert.Equal(ScanDenialReason.UserInactive, scan.Reason);
        Assert.Equal(BookingStatus.Booked, (await GetBookingAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task InactiveRoom_IsDenied_EvenWithABooking()
    {
        var (user, room) = await ArrangeAsync();
        var now = DateTimeOffset.UtcNow;
        var booking = await CreateBookingAsync(room.Id, user.Id, now.AddMinutes(5), now.AddMinutes(65));
        await DeactivateAsync("rooms", room.Id);

        var scan = await ScanResultAsync(user.Id);

        Assert.Equal(ScanDenialReason.RoomInactive, scan.Reason);
        Assert.Equal(BookingStatus.Booked, (await GetBookingAsync(booking.Id)).Status);
    }

    [Theory]
    [InlineData("reader-unknown")]
    [InlineData("not%20a%20valid%20id!")]
    public async Task UnknownOrMalformedReader_ReturnsNotFoundProblem(string readerId)
    {
        var (user, _) = await ArrangeAsync();

        var response = await ScanAsync(user.Id, readerId);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "reader_not_found");
    }

    [Fact]
    public async Task AfterTheReaderWasRemoved_ScansAreNotFound()
    {
        var (user, room) = await ArrangeAsync();
        await Client.DeleteAsync($"{ApiRoot}/rooms/{room.Id}/reader");

        var response = await ScanAsync(user.Id);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "reader_not_found");
    }

    [Theory]
    [InlineData("{}")] // no user id
    [InlineData("""{ "userId": "not-a-guid" }""")]
    [InlineData("not json")]
    public async Task UnreadableBody_ReturnsBadRequestProblem(string body)
    {
        await ArrangeAsync();

        var response = await Client.PostAsync(
            $"{ApiRoot}/readers/{ReaderId}/scans", new StringContent(body, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "bad_request");
    }
}
