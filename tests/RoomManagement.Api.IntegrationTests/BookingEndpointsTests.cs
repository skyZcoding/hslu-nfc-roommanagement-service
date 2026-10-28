using System.Net;
using System.Net.Http.Json;
using System.Text;
using RoomManagement.Application.Bookings;
using RoomManagement.Domain.Bookings;

namespace RoomManagement.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingEndpointsTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string RoomsUrl = $"{ApiRoot}/rooms";
    private const string BookingsUrl = $"{ApiRoot}/bookings";

    private static readonly DateTimeOffset Start = new(2030, 6, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(1);

    private Task<HttpResponseMessage> PostBookingAsync(Guid roomId, object body) =>
        Client.PostAsJsonAsync($"{RoomsUrl}/{roomId}/bookings", body, Json);

    private Task<HttpResponseMessage> PostBookingAsync(Guid roomId, Guid userId, DateTimeOffset start, DateTimeOffset end) =>
        PostBookingAsync(roomId, new { userId, start, end });

    private async Task<BookingDto> GetBookingAsync(Guid id) =>
        await ReadAsync<BookingDto>(await Client.GetAsync($"{BookingsUrl}/{id}"));

    private async Task<Guid[]> BookingIdsAsync(string url) =>
        (await ReadAsync<List<BookingDto>>(await Client.GetAsync(url))).Select(b => b.Id).ToArray();

    // ---- create ----

    [Fact]
    public async Task Create_ReturnsCreatedWithLocation_AndTheBookingCanBeFetched()
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();

        var response = await PostBookingAsync(room.Id, user.Id, Start, End);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Booked\"", json);
        Assert.Contains("\"checkedInAt\":null", json);
        var created = System.Text.Json.JsonSerializer.Deserialize<BookingDto>(json, Json)!;
        Assert.Equal(room.Id, created.RoomId);
        Assert.Equal(user.Id, created.UserId);
        Assert.Equal(Start, created.Start);
        Assert.Equal(End, created.End);
        Assert.Equal(BookingStatus.Booked, created.Status);
        Assert.EndsWith($"{BookingsUrl}/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal(created, await ReadAsync<BookingDto>(await Client.GetAsync(response.Headers.Location)));
    }

    [Fact]
    public async Task Create_TimesWithAnOffset_AreComparedAsInstantsAndStoredAsUtc()
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        var plusTwo = TimeSpan.FromHours(2);

        var response = await PostBookingAsync(room.Id, user.Id, Start.ToOffset(plusTwo), End.ToOffset(plusTwo));
        var overlapping = await PostBookingAsync(room.Id, user.Id, Start.AddMinutes(30), End.AddMinutes(30));

        var created = await ReadAsync<BookingDto>(response);
        Assert.Equal(TimeSpan.Zero, created.Start.Offset);
        Assert.Equal(Start, created.Start);
        await AssertProblemAsync(overlapping, HttpStatusCode.Conflict, "booking_overlap");
    }

    [Fact]
    public async Task Create_InvalidBody_ReturnsValidationProblemWithCamelCaseFieldNames()
    {
        var room = await CreateRoomAsync();

        var response = await PostBookingAsync(room.Id, Guid.Empty, End, Start);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Contains("userId", problem.Errors.Keys);
        Assert.Contains("end", problem.Errors.Keys);
        Assert.DoesNotContain("start", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_MissingProperty_ReturnsBadRequestProblem()
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        var body = $$"""{ "userId": "{{user.Id}}", "start": "{{Start:O}}" }""";

        var response = await Client.PostAsync(
            $"{RoomsUrl}/{room.Id}/bookings", new StringContent(body, Encoding.UTF8, "application/json"));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "bad_request");
        Assert.Contains("end", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_UnknownRoom_ReturnsNotFoundProblem()
    {
        var user = await CreateUserAsync();

        var response = await PostBookingAsync(Guid.NewGuid(), user.Id, Start, End);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "room_not_found");
    }

    [Fact]
    public async Task Create_UnknownUser_ReturnsValidationProblemOnUserId()
    {
        var room = await CreateRoomAsync();

        var response = await PostBookingAsync(room.Id, Guid.NewGuid(), Start, End);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Contains("userId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_InactiveRoom_ReturnsConflict()
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        await DeactivateAsync("rooms", room.Id);

        var response = await PostBookingAsync(room.Id, user.Id, Start, End);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "room_inactive");
    }

    [Fact]
    public async Task Create_InactiveUser_ReturnsConflict()
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        await DeactivateAsync("users", user.Id);

        var response = await PostBookingAsync(room.Id, user.Id, Start, End);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "user_inactive");
    }

    [Theory]
    [InlineData(-30, 30, HttpStatusCode.Conflict)] // overlaps the beginning
    [InlineData(0, 60, HttpStatusCode.Conflict)] // identical
    [InlineData(15, 45, HttpStatusCode.Conflict)] // contained
    [InlineData(30, 90, HttpStatusCode.Conflict)] // overlaps the end
    [InlineData(-60, 0, HttpStatusCode.Created)] // directly before
    [InlineData(60, 120, HttpStatusCode.Created)] // directly after
    public async Task Create_NextToAnExistingBooking_IsOnlyAllowedWithoutOverlap(
        int fromMinutes, int toMinutes, HttpStatusCode expected)
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        await CreateBookingAsync(room.Id, user.Id, Start, End);

        var response = await PostBookingAsync(room.Id, user.Id, Start.AddMinutes(fromMinutes), Start.AddMinutes(toMinutes));

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Conflict)
            await AssertProblemAsync(response, HttpStatusCode.Conflict, "booking_overlap");
    }

    [Fact]
    public async Task Create_AfterTheOverlappingBookingWasCancelled_Succeeds()
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        var first = await CreateBookingAsync(room.Id, user.Id, Start, End);
        await Client.PostAsync($"{BookingsUrl}/{first.Id}/cancel", content: null);

        var response = await PostBookingAsync(room.Id, user.Id, Start, End);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameSlotInAnotherRoom_Succeeds()
    {
        var room = await CreateRoomAsync("H2.301");
        var otherRoom = await CreateRoomAsync("H2.302");
        var user = await CreateUserAsync();
        await CreateBookingAsync(room.Id, user.Id, Start, End);

        var response = await PostBookingAsync(otherRoom.Id, user.Id, Start, End);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---- get ----

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{BookingsUrl}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "booking_not_found");
    }

    [Fact]
    public async Task Get_IdThatIsNotAGuid_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{BookingsUrl}/not-a-guid");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    // ---- cancel ----

    [Fact]
    public async Task Cancel_ReturnsNoContent_AndMarksTheBookingCancelled()
    {
        var booking = await ArrangeBookingAsync(Start, End);

        var response = await Client.PostAsync($"{BookingsUrl}/{booking.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(BookingStatus.Cancelled, (await GetBookingAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledBooking_ReturnsConflict()
    {
        var booking = await ArrangeBookingAsync(Start, End);
        await Client.PostAsync($"{BookingsUrl}/{booking.Id}/cancel", content: null);

        var response = await Client.PostAsync($"{BookingsUrl}/{booking.Id}/cancel", content: null);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "booking_invalid_state");
    }

    [Fact]
    public async Task Cancel_CheckedInBooking_ReturnsConflict_AndKeepsIt()
    {
        var now = DateTimeOffset.UtcNow;
        var booking = await ArrangeBookingAsync(now.AddMinutes(5), now.AddMinutes(65));
        await Client.PostAsync($"{BookingsUrl}/{booking.Id}/check-in", content: null);

        var response = await Client.PostAsync($"{BookingsUrl}/{booking.Id}/cancel", content: null);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "booking_invalid_state");
        Assert.Equal(BookingStatus.CheckedIn, (await GetBookingAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task Cancel_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.PostAsync($"{BookingsUrl}/{Guid.NewGuid()}/cancel", content: null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "booking_not_found");
    }

    // ---- manual check-in ----

    [Fact]
    public async Task CheckIn_InsideTheWindow_ReturnsNoContent_AndRecordsTheTime()
    {
        var before = DateTimeOffset.UtcNow;
        var booking = await ArrangeBookingAsync(before.AddMinutes(5), before.AddMinutes(65));

        var response = await Client.PostAsync($"{BookingsUrl}/{booking.Id}/check-in", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var checkedIn = await GetBookingAsync(booking.Id);
        Assert.Equal(BookingStatus.CheckedIn, checkedIn.Status);
        Assert.InRange(checkedIn.CheckedInAt!.Value, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task CheckIn_TwiceOrCancelledOrBeforeTheWindowOpens_ReturnsConflict()
    {
        var now = DateTimeOffset.UtcNow;
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        var running = await CreateBookingAsync(room.Id, user.Id, now.AddMinutes(5), now.AddMinutes(65));
        var cancelled = await CreateBookingAsync(room.Id, user.Id, now.AddMinutes(65), now.AddMinutes(125));
        var later = await CreateBookingAsync(room.Id, user.Id, now.AddHours(3), now.AddHours(4));
        await Client.PostAsync($"{BookingsUrl}/{running.Id}/check-in", content: null);
        await Client.PostAsync($"{BookingsUrl}/{cancelled.Id}/cancel", content: null);

        foreach (var booking in new[] { running, cancelled, later })
        {
            var response = await Client.PostAsync($"{BookingsUrl}/{booking.Id}/check-in", content: null);
            await AssertProblemAsync(response, HttpStatusCode.Conflict, "booking_invalid_state");
        }

        Assert.Equal(BookingStatus.Booked, (await GetBookingAsync(later.Id)).Status);
    }

    [Fact]
    public async Task CheckIn_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await Client.PostAsync($"{BookingsUrl}/{Guid.NewGuid()}/check-in", content: null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "booking_not_found");
    }

    // ---- lists ----

    [Fact]
    public async Task ListByRoom_ReturnsEveryStatusOrderedByStart_AndOnlyThatRoom()
    {
        var room = await CreateRoomAsync("H2.301");
        var otherRoom = await CreateRoomAsync("H2.302");
        var user = await CreateUserAsync();
        var second = await CreateBookingAsync(room.Id, user.Id, End, End.AddHours(1));
        var first = await CreateBookingAsync(room.Id, user.Id, Start, End);
        await CreateBookingAsync(otherRoom.Id, user.Id, Start, End);
        await Client.PostAsync($"{BookingsUrl}/{first.Id}/cancel", content: null);

        var ids = await BookingIdsAsync($"{RoomsUrl}/{room.Id}/bookings");

        Assert.Equal([first.Id, second.Id], ids);
    }

    [Fact]
    public async Task ListByRoom_WithRange_ReturnsOnlyBookingsOverlappingIt()
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        var morning = await CreateBookingAsync(room.Id, user.Id, Start, End);
        var noon = await CreateBookingAsync(room.Id, user.Id, End, End.AddHours(1));
        var nextDay = await CreateBookingAsync(room.Id, user.Id, Start.AddDays(1), End.AddDays(1));
        var url = $"{RoomsUrl}/{room.Id}/bookings";

        Assert.Equal([noon.Id, nextDay.Id], await BookingIdsAsync($"{url}?from={Iso(End)}"));
        Assert.Equal([morning.Id], await BookingIdsAsync($"{url}?to={Iso(End)}"));
        Assert.Equal([noon.Id], await BookingIdsAsync($"{url}?from={Iso(End)}&to={Iso(End.AddHours(1))}"));
        Assert.Equal([morning.Id, noon.Id, nextDay.Id], await BookingIdsAsync(url));
    }

    [Fact]
    public async Task ListByRoom_RoomWithoutBookings_ReturnsAnEmptyArray()
    {
        var room = await CreateRoomAsync();

        var response = await Client.GetAsync($"{RoomsUrl}/{room.Id}/bookings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ListByRoom_UnknownRoom_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{RoomsUrl}/{Guid.NewGuid()}/bookings");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "room_not_found");
    }

    [Fact]
    public async Task ListByRoom_ToNotAfterFrom_ReturnsValidationProblem()
    {
        var room = await CreateRoomAsync();

        var response = await Client.GetAsync($"{RoomsUrl}/{room.Id}/bookings?from={Iso(End)}&to={Iso(Start)}");

        var problem = await AssertValidationProblemAsync(response);
        Assert.Contains("to", problem.Errors.Keys);
    }

    [Fact]
    public async Task ListByRoom_DateThatCannotBeParsed_ReturnsBadRequestProblem()
    {
        var room = await CreateRoomAsync();

        var response = await Client.GetAsync($"{RoomsUrl}/{room.Id}/bookings?from=yesterday");

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "bad_request");
        Assert.Contains("from", problem.Detail);
    }

    [Fact]
    public async Task ListByUser_ReturnsTheUsersBookingsAcrossRoomsOrderedByStart()
    {
        var room = await CreateRoomAsync("H2.301");
        var otherRoom = await CreateRoomAsync("H2.302");
        var jan = await CreateUserAsync("jan@example.com");
        var anna = await CreateUserAsync("anna@example.com", "Anna");
        var later = await CreateBookingAsync(otherRoom.Id, jan.Id, End, End.AddHours(1));
        var earlier = await CreateBookingAsync(room.Id, jan.Id, Start, End);
        await CreateBookingAsync(room.Id, anna.Id, End, End.AddHours(1));
        var url = $"{ApiRoot}/users/{jan.Id}/bookings";

        Assert.Equal([earlier.Id, later.Id], await BookingIdsAsync(url));
        Assert.Equal([later.Id], await BookingIdsAsync($"{url}?from={Iso(End)}"));
    }

    [Fact]
    public async Task ListByUser_UnknownUser_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{ApiRoot}/users/{Guid.NewGuid()}/bookings");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "user_not_found");
    }

    // ---- availability ----

    [Theory]
    [InlineData(0, 60, false)] // the booked slot
    [InlineData(30, 90, false)] // overlaps its end
    [InlineData(60, 120, true)] // right after
    [InlineData(-60, 0, true)] // right before
    public async Task Availability_ReflectsExistingBookings(int fromMinutes, int toMinutes, bool expected)
    {
        var room = await CreateRoomAsync();
        await CreateBookingAsync(room.Id, (await CreateUserAsync()).Id, Start, End);
        var from = Start.AddMinutes(fromMinutes);
        var to = Start.AddMinutes(toMinutes);

        var response = await Client.GetAsync($"{RoomsUrl}/{room.Id}/availability?from={Iso(from)}&to={Iso(to)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availability = await ReadAsync<RoomAvailabilityDto>(response);
        Assert.Equal(expected, availability.IsAvailable);
        Assert.Equal(room.Id, availability.RoomId);
        Assert.Equal(from, availability.From);
        Assert.Equal(to, availability.To);
    }

    [Fact]
    public async Task Availability_SlotOfACancelledBooking_IsAvailable()
    {
        var room = await CreateRoomAsync();
        var booking = await CreateBookingAsync(room.Id, (await CreateUserAsync()).Id, Start, End);
        await Client.PostAsync($"{BookingsUrl}/{booking.Id}/cancel", content: null);

        var response = await Client.GetAsync($"{RoomsUrl}/{room.Id}/availability?from={Iso(Start)}&to={Iso(End)}");

        Assert.True((await ReadAsync<RoomAvailabilityDto>(response)).IsAvailable);
    }

    [Fact]
    public async Task Availability_InactiveRoom_IsNeverAvailable()
    {
        var room = await CreateRoomAsync();
        await DeactivateAsync("rooms", room.Id);

        var response = await Client.GetAsync($"{RoomsUrl}/{room.Id}/availability?from={Iso(Start)}&to={Iso(End)}");

        Assert.False((await ReadAsync<RoomAvailabilityDto>(response)).IsAvailable);
    }

    [Fact]
    public async Task Availability_MissingToOrInvertedRange_ReturnsProblem()
    {
        var room = await CreateRoomAsync();
        var url = $"{RoomsUrl}/{room.Id}/availability";

        var missing = await Client.GetAsync($"{url}?from={Iso(Start)}");
        var inverted = await Client.GetAsync($"{url}?from={Iso(End)}&to={Iso(Start)}");

        var missingProblem = await AssertProblemAsync(missing, HttpStatusCode.BadRequest, "bad_request");
        Assert.Contains("to", missingProblem.Detail);
        var invertedProblem = await AssertValidationProblemAsync(inverted);
        Assert.Contains("to", invertedProblem.Errors.Keys);
    }

    [Fact]
    public async Task Availability_UnknownRoom_ReturnsNotFoundProblem()
    {
        var response = await Client.GetAsync($"{RoomsUrl}/{Guid.NewGuid()}/availability?from={Iso(Start)}&to={Iso(End)}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "room_not_found");
    }

    private async Task<BookingDto> ArrangeBookingAsync(DateTimeOffset start, DateTimeOffset end)
    {
        var room = await CreateRoomAsync();
        var user = await CreateUserAsync();
        return await CreateBookingAsync(room.Id, user.Id, start, end);
    }
}
