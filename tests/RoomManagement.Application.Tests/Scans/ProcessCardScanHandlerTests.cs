using RoomManagement.Application.Common;
using RoomManagement.Application.Scans;
using RoomManagement.Application.Tests.Bookings;
using RoomManagement.Domain.Bookings;

namespace RoomManagement.Application.Tests.Scans;

public class ProcessCardScanHandlerTests : BookingTestBase
{
    private const string ReaderId = "reader-1";

    private ProcessCardScanHandler Handler() => new(Rooms, Users, Bookings, UnitOfWork, Clock);

    private Task<Result<CardScanResult>> Scan(Guid userId, string? readerId = ReaderId) =>
        Handler().HandleAsync(readerId, new CardScanRequest(userId), Ct);

    private static void AssertDenied(Result<CardScanResult> result, ScanDenialReason reason)
    {
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Granted);
        Assert.Equal(reason, result.Value.Reason);
        Assert.Null(result.Value.BookingId);
    }

    // ---- granted ----

    [Fact]
    public async Task BookingWithOpenWindow_IsCheckedInAndGranted()
    {
        var room = SeedRoom();
        var user = SeedUser(displayName: "Jan Walker");
        var booking = SeedBooking(room, user);
        Clock.UtcNow = Start.AddMinutes(-5);

        var result = await Scan(user.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Granted);
        Assert.Null(result.Value.Reason);
        Assert.Equal(room.Id, result.Value.RoomId);
        Assert.Equal(booking.Id, result.Value.BookingId);
        Assert.Equal("Jan Walker", result.Value.UserDisplayName);
        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
        Assert.Equal(Clock.UtcNow, booking.CheckedInAt);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ScanDuringTheBooking_IsGranted()
    {
        var room = SeedRoom();
        var user = SeedUser();
        SeedBooking(room, user);
        Clock.UtcNow = Start.AddMinutes(45);

        var result = await Scan(user.Id);

        Assert.True(result.Value.Granted);
    }

    [Fact]
    public async Task ReaderId_IsMatchedIgnoringCaseAndWhitespace()
    {
        var room = SeedRoom(reader: "reader-1");
        var user = SeedUser();
        SeedBooking(room, user);

        var result = await Scan(user.Id, " READER-1 ");

        Assert.True(result.Value.Granted);
    }

    [Fact]
    public async Task BackToBackBookings_CheckInTheEarliestUnusedOne()
    {
        var room = SeedRoom();
        var user = SeedUser();
        var first = SeedBooking(room, user);
        var second = SeedBooking(room, user, End, End.AddHours(1));
        Clock.UtcNow = End.AddMinutes(-5); // both windows are open

        var result = await Scan(user.Id);

        Assert.Equal(first.Id, result.Value.BookingId);
        Assert.Equal(BookingStatus.CheckedIn, first.Status);
        Assert.Equal(BookingStatus.Booked, second.Status);
    }

    [Fact]
    public async Task BackToBackBookings_SecondScanChecksInTheNextOne_ThenReportsAlreadyCheckedIn()
    {
        var room = SeedRoom();
        var user = SeedUser();
        var first = SeedBooking(room, user);
        var second = SeedBooking(room, user, End, End.AddHours(1));
        Clock.UtcNow = End.AddMinutes(-5);

        var firstScan = await Scan(user.Id);
        var secondScan = await Scan(user.Id);
        var thirdScan = await Scan(user.Id);

        Assert.Equal(first.Id, firstScan.Value.BookingId);
        Assert.Equal(second.Id, secondScan.Value.BookingId);
        AssertDenied(thirdScan, ScanDenialReason.AlreadyCheckedIn);
    }

    // ---- denied: booking ----

    [Fact]
    public async Task AlreadyCheckedInBooking_IsDeniedAsAlreadyCheckedIn()
    {
        var room = SeedRoom();
        var user = SeedUser(displayName: "Jan Walker");
        SeedBooking(room, user).CheckIn(Start);
        Clock.UtcNow = Start.AddMinutes(20);

        var result = await Scan(user.Id);

        AssertDenied(result, ScanDenialReason.AlreadyCheckedIn);
        Assert.Equal("Jan Walker", result.Value.UserDisplayName);
        Assert.Equal(room.Id, result.Value.RoomId);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UserWithoutAnyBooking_IsDenied_AndNothingIsCreated()
    {
        SeedRoom();
        var user = SeedUser(displayName: "Jan Walker");

        var result = await Scan(user.Id);

        AssertDenied(result, ScanDenialReason.NoActiveBooking);
        Assert.Equal("Jan Walker", result.Value.UserDisplayName);
        Assert.Empty(Bookings.Items);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(-11)] // window not open yet
    [InlineData(60)] // booking is over
    public async Task ScanOutsideTheWindow_IsDenied(int minutesFromStart)
    {
        var room = SeedRoom();
        var user = SeedUser();
        var booking = SeedBooking(room, user);
        Clock.UtcNow = Start.AddMinutes(minutesFromStart);

        var result = await Scan(user.Id);

        AssertDenied(result, ScanDenialReason.NoActiveBooking);
        Assert.Equal(BookingStatus.Booked, booking.Status);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelledBooking_IsDenied()
    {
        var room = SeedRoom();
        var user = SeedUser();
        SeedBooking(room, user).Cancel();

        var result = await Scan(user.Id);

        AssertDenied(result, ScanDenialReason.NoActiveBooking);
    }

    [Fact]
    public async Task BookingOfAnotherUser_IsDenied_AndStaysUntouched()
    {
        var room = SeedRoom();
        var owner = SeedUser("owner@example.com");
        var other = SeedUser("other@example.com");
        var booking = SeedBooking(room, owner);

        var result = await Scan(other.Id);

        AssertDenied(result, ScanDenialReason.NoActiveBooking);
        Assert.Equal(BookingStatus.Booked, booking.Status);
    }

    [Fact]
    public async Task BookingInAnotherRoom_IsDenied()
    {
        SeedRoom("H2.301", reader: ReaderId);
        var otherRoom = SeedRoom("H2.302", reader: "reader-2");
        var user = SeedUser();
        var booking = SeedBooking(otherRoom, user);

        var result = await Scan(user.Id);

        AssertDenied(result, ScanDenialReason.NoActiveBooking);
        Assert.Equal(BookingStatus.Booked, booking.Status);
    }

    // ---- denied: user and room ----

    [Fact]
    public async Task UnknownUser_IsDeniedAsUserUnknown()
    {
        var room = SeedRoom();

        var result = await Scan(Guid.NewGuid());

        AssertDenied(result, ScanDenialReason.UserUnknown);
        Assert.Equal(room.Id, result.Value.RoomId);
        Assert.Null(result.Value.UserDisplayName);
    }

    [Fact]
    public async Task EmptyUserId_IsDeniedAsUserUnknown()
    {
        SeedRoom();

        var result = await Scan(Guid.Empty);

        AssertDenied(result, ScanDenialReason.UserUnknown);
    }

    [Fact]
    public async Task InactiveUser_IsDenied_EvenWithABooking()
    {
        var room = SeedRoom();
        var user = SeedUser(active: false);
        var booking = SeedBooking(room, user);

        var result = await Scan(user.Id);

        AssertDenied(result, ScanDenialReason.UserInactive);
        Assert.Equal(BookingStatus.Booked, booking.Status);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task InactiveRoom_IsDenied_EvenWithABooking()
    {
        var room = SeedRoom(active: false);
        var user = SeedUser();
        var booking = SeedBooking(room, user);

        var result = await Scan(user.Id);

        AssertDenied(result, ScanDenialReason.RoomInactive);
        Assert.Equal(room.Id, result.Value.RoomId);
        Assert.Equal(BookingStatus.Booked, booking.Status);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    // ---- unknown reader ----

    [Theory]
    [InlineData("reader-9")]
    [InlineData("not a valid id!")]
    [InlineData("")]
    [InlineData(null)]
    public async Task UnknownOrMalformedReader_ReturnsNotFound(string? readerId)
    {
        SeedRoom();
        var user = SeedUser();

        var result = await Scan(user.Id, readerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("reader_not_found", result.Error.Code);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }
}
