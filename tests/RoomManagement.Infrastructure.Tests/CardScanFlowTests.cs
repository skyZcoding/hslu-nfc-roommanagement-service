using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Application.Bookings;
using RoomManagement.Application.Common;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Scans;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Users;

namespace RoomManagement.Infrastructure.Tests;

/// <summary>The application handlers wired to the real repositories and database, one handler call per "request".</summary>
[Collection(PostgresCollection.Name)]
public class CardScanFlowTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private const string ReaderId = "reader-h2-301";

    private async Task<(UserDto User, RoomDto Room)> CreateUserAndRoomWithReaderAsync()
    {
        var user = (await Run<CreateUserHandler, Result<UserDto>>(
            h => h.HandleAsync(new("jan@example.com", "Jan Walker", UserRole.Member), Ct))).Value;
        var room = (await Run<CreateRoomHandler, Result<RoomDto>>(
            h => h.HandleAsync(new("H2.301", "Campus Rotkreuz", 3, 24), Ct))).Value;
        var assigned = await Run<AssignReaderHandler, Result>(h => h.HandleAsync(room.Id, new(ReaderId), Ct));
        Assert.True(assigned.IsSuccess);

        return (user, room);
    }

    private Task<Result<BookingDto>> BookAsync(RoomDto room, UserDto user, DateTimeOffset start, DateTimeOffset end) =>
        Run<CreateBookingHandler, Result<BookingDto>>(h => h.HandleAsync(room.Id, new(user.Id, start, end), Ct));

    private Task<Result<CardScanResult>> ScanAsync(Guid userId, string readerId = ReaderId) =>
        Run<ProcessCardScanHandler, Result<CardScanResult>>(h => h.HandleAsync(readerId, new(userId), Ct));

    [Fact]
    public async Task UserWithABooking_ChecksInByHoldingTheCardToTheRoomsReader()
    {
        var (user, room) = await CreateUserAndRoomWithReaderAsync();
        var now = DateTimeOffset.UtcNow;
        var booking = (await BookAsync(room, user, now.AddMinutes(5), now.AddMinutes(65))).Value;

        var firstScan = await ScanAsync(user.Id);
        var secondScan = await ScanAsync(user.Id);
        var stored = await Run<GetBookingHandler, Result<BookingDto>>(h => h.HandleAsync(booking.Id, Ct));

        Assert.True(firstScan.Value.Granted);
        Assert.Equal(booking.Id, firstScan.Value.BookingId);
        Assert.Equal(room.Id, firstScan.Value.RoomId);
        Assert.Equal("Jan Walker", firstScan.Value.UserDisplayName);
        Assert.False(secondScan.Value.Granted);
        Assert.Equal(ScanDenialReason.AlreadyCheckedIn, secondScan.Value.Reason);
        Assert.Equal(BookingStatus.CheckedIn, stored.Value.Status);
        Assert.NotNull(stored.Value.CheckedInAt);
    }

    [Fact]
    public async Task UserWithoutABooking_IsDenied_AndNoBookingIsCreated()
    {
        var (user, room) = await CreateUserAndRoomWithReaderAsync();

        var scan = await ScanAsync(user.Id);
        var bookings = await Run<ListRoomBookingsHandler, Result<IReadOnlyList<BookingDto>>>(
            h => h.HandleAsync(room.Id, new ListBookingsQuery(), Ct));

        Assert.False(scan.Value.Granted);
        Assert.Equal(ScanDenialReason.NoActiveBooking, scan.Value.Reason);
        Assert.Empty(bookings.Value);
    }

    [Fact]
    public async Task BookingThatStartsLaterToday_IsNotCheckedInYet()
    {
        var (user, room) = await CreateUserAndRoomWithReaderAsync();
        var now = DateTimeOffset.UtcNow;
        await BookAsync(room, user, now.AddHours(2), now.AddHours(3));

        var scan = await ScanAsync(user.Id);

        Assert.Equal(ScanDenialReason.NoActiveBooking, scan.Value.Reason);
    }

    [Fact]
    public async Task ScanAtAnUnknownReader_IsNotFound()
    {
        var (user, _) = await CreateUserAndRoomWithReaderAsync();

        var scan = await ScanAsync(user.Id, "reader-unknown");

        Assert.Equal(ErrorType.NotFound, scan.Error!.Type);
    }

    [Fact]
    public async Task ReaderLookup_FindsTheRoom_AndStopsFindingItOnceTheReaderIsRemoved()
    {
        var (_, room) = await CreateUserAndRoomWithReaderAsync();

        var found = await Run<GetRoomByReaderHandler, Result<RoomDto>>(h => h.HandleAsync(" READER-H2-301", Ct));
        await Run<RemoveReaderHandler, Result>(h => h.HandleAsync(room.Id, Ct));
        var afterRemoval = await Run<GetRoomByReaderHandler, Result<RoomDto>>(h => h.HandleAsync(ReaderId, Ct));

        Assert.Equal(room.Id, found.Value.Id);
        Assert.Equal(ErrorType.NotFound, afterRemoval.Error!.Type);
    }

    [Fact]
    public async Task OverlappingBooking_IsRejectedByTheHandler()
    {
        var (user, room) = await CreateUserAndRoomWithReaderAsync();
        var now = DateTimeOffset.UtcNow;
        await BookAsync(room, user, now.AddHours(1), now.AddHours(2));

        var overlapping = await BookAsync(room, user, now.AddMinutes(90), now.AddMinutes(150));
        var backToBack = await BookAsync(room, user, now.AddHours(2), now.AddHours(3));

        Assert.Equal("booking_overlap", overlapping.Error!.Code);
        Assert.True(backToBack.IsSuccess);
    }

    [Fact]
    public async Task Availability_ReflectsExistingBookings()
    {
        var (user, room) = await CreateUserAndRoomWithReaderAsync();
        var now = DateTimeOffset.UtcNow;
        await BookAsync(room, user, now.AddHours(1), now.AddHours(2));

        var busy = await Run<GetRoomAvailabilityHandler, Result<RoomAvailabilityDto>>(
            h => h.HandleAsync(room.Id, new(now.AddMinutes(90), now.AddMinutes(150)), Ct));
        var free = await Run<GetRoomAvailabilityHandler, Result<RoomAvailabilityDto>>(
            h => h.HandleAsync(room.Id, new(now.AddHours(2), now.AddHours(3)), Ct));

        Assert.False(busy.Value.IsAvailable);
        Assert.True(free.Value.IsAvailable);
    }

    [Fact]
    public async Task EveryHandler_CanBeResolvedFromTheRealContainer()
    {
        var handlerTypes = typeof(Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .ToList();

        await InScope(sp =>
        {
            Assert.NotEmpty(handlerTypes);
            Assert.All(handlerTypes, type => Assert.NotNull(sp.GetRequiredService(type)));
            return Task.CompletedTask;
        });
    }
}
