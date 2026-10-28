using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Application.Bookings;
using RoomManagement.Application.Common;
using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Rooms;
using RoomManagement.Domain.Users;

namespace RoomManagement.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public class BookingRepositoryTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private readonly Room _room = NewRoom("H2.301", reader: "reader-1");
    private readonly Room _otherRoom = NewRoom("H2.302", reader: "reader-2");
    private readonly User _user = NewUser("jan@example.com");
    private readonly User _otherUser = NewUser("anna@example.com", "Anna");

    private Task SeedRoomsAndUsersAsync() => SeedAsync(_room, _otherRoom, _user, _otherUser);

    private Task AddAsync(Booking booking) => InScope(async sp =>
    {
        sp.GetRequiredService<IBookingRepository>().Add(booking);
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    });

    private Task<Booking?> GetAsync(Guid id) =>
        InScope(sp => sp.GetRequiredService<IBookingRepository>().GetByIdAsync(id, Ct));

    private Task<IReadOnlyList<Booking>> ListByRoom(Guid roomId, DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        InScope(sp => sp.GetRequiredService<IBookingRepository>().ListByRoomAsync(roomId, from, to, Ct));

    private Task<IReadOnlyList<Booking>> ListByUser(Guid userId, DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        InScope(sp => sp.GetRequiredService<IBookingRepository>().ListByUserAsync(userId, from, to, Ct));

    private Task<IReadOnlyList<Booking>> ListCheckInCandidates(DateTimeOffset now) =>
        InScope(sp => sp.GetRequiredService<IBookingRepository>().ListWithOpenCheckInWindowAsync(_room.Id, _user.Id, now, Ct));

    // ---- mapping ----

    [Fact]
    public async Task Add_ThenGetById_RoundTripsAllFields()
    {
        await SeedRoomsAndUsersAsync();
        var booking = NewBooking(_room, _user);
        booking.CheckIn(Start);

        await AddAsync(booking);
        var loaded = await GetAsync(booking.Id);

        Assert.NotNull(loaded);
        Assert.Equal(_room.Id, loaded.RoomId);
        Assert.Equal(_user.Id, loaded.UserId);
        Assert.Equal(Start, loaded.Start);
        Assert.Equal(End, loaded.End);
        Assert.Equal(BookingStatus.CheckedIn, loaded.Status);
        Assert.Equal(Start, loaded.CheckedInAt);
        Assert.Equal(TimeSpan.Zero, loaded.Start.Offset);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(await GetAsync(Guid.NewGuid()));
    }

    // ---- listing ----

    [Fact]
    public async Task ListByRoom_ReturnsOverlappingBookingsOfThatRoomOrderedByStart()
    {
        await SeedRoomsAndUsersAsync();
        var second = NewBooking(_room, _user, End, End.AddHours(1));
        var first = NewBooking(_room, _user);
        var cancelled = NewBooking(_room, _otherUser, End.AddHours(1), End.AddHours(2));
        cancelled.Cancel();
        await SeedAsync(second, first, cancelled, NewBooking(_otherRoom, _user));

        var all = await ListByRoom(_room.Id);
        var firstOnly = await ListByRoom(_room.Id, to: End);
        var secondOnly = await ListByRoom(_room.Id, from: End, to: End.AddHours(1));
        var none = await ListByRoom(_room.Id, from: End.AddHours(2));

        Assert.Equal([first.Id, second.Id, cancelled.Id], all.Select(b => b.Id));
        Assert.Equal([first.Id], firstOnly.Select(b => b.Id));
        Assert.Equal([second.Id], secondOnly.Select(b => b.Id)); // [from, to) is half-open on both sides
        Assert.Empty(none);
    }

    [Fact]
    public async Task ListByRoom_AcceptsBoundsWithAnOffset()
    {
        await SeedRoomsAndUsersAsync();
        var booking = NewBooking(_room, _user);
        await SeedAsync(booking);
        var plusTwo = TimeSpan.FromHours(2);

        // 11:30 +02:00 is 09:30 UTC, inside the booking; 12:00 +02:00 is 10:00 UTC, exactly its end.
        var overlapping = await ListByRoom(_room.Id, Start.ToOffset(plusTwo).AddMinutes(30), Start.ToOffset(plusTwo).AddHours(2));
        var after = await ListByRoom(_room.Id, End.ToOffset(plusTwo), End.ToOffset(plusTwo).AddHours(1));

        Assert.Equal([booking.Id], overlapping.Select(b => b.Id));
        Assert.Empty(after);
    }

    [Fact]
    public async Task ListByUser_ReturnsTheUsersBookingsAcrossRooms()
    {
        await SeedRoomsAndUsersAsync();
        var inRoom = NewBooking(_room, _user);
        var inOtherRoom = NewBooking(_otherRoom, _user, End, End.AddHours(1));
        await SeedAsync(inRoom, inOtherRoom, NewBooking(_room, _otherUser, End, End.AddHours(1)));

        var all = await ListByUser(_user.Id);
        var fromEnd = await ListByUser(_user.Id, from: End);

        Assert.Equal([inRoom.Id, inOtherRoom.Id], all.Select(b => b.Id));
        Assert.Equal([inOtherRoom.Id], fromEnd.Select(b => b.Id));
    }

    // ---- check-in window ----

    [Theory]
    [InlineData(-11, 0, false)] // 08:49: window not open yet
    [InlineData(-10, 0, true)] // 08:50: window opens
    [InlineData(0, 0, true)] // 09:00: booking starts
    [InlineData(59, 59, true)] // 09:59:59: last moment
    [InlineData(60, 0, false)] // 10:00: booking over
    public async Task ListWithOpenCheckInWindow_FollowsTheDomainWindow(int minutes, int seconds, bool expected)
    {
        await SeedRoomsAndUsersAsync();
        var booking = NewBooking(_room, _user);
        await SeedAsync(booking);
        var now = Start.AddMinutes(minutes).AddSeconds(seconds);

        var candidates = await ListCheckInCandidates(now);

        Assert.Equal(expected, candidates.Any());
        Assert.Equal(expected, booking.IsCheckInWindowOpen(now)); // the query and the domain rule agree
    }

    [Fact]
    public async Task ListWithOpenCheckInWindow_SkipsOtherUsersAndOtherRooms()
    {
        await SeedRoomsAndUsersAsync();
        var mine = NewBooking(_room, _user);
        var otherUserSameRoom = NewBooking(_room, _otherUser, End, End.AddHours(1)); // window opens 09:50
        var sameUserOtherRoom = NewBooking(_otherRoom, _user);
        await SeedAsync(mine, otherUserSameRoom, sameUserOtherRoom);

        var candidates = await ListCheckInCandidates(Start.AddMinutes(55));

        Assert.Equal([mine.Id], candidates.Select(b => b.Id));
    }

    [Fact]
    public async Task ListWithOpenCheckInWindow_SkipsCancelledButKeepsCheckedInBookings()
    {
        await SeedRoomsAndUsersAsync();
        var cancelled = NewBooking(_room, _user);
        cancelled.Cancel();
        var checkedIn = NewBooking(_room, _user); // the cancelled booking no longer blocks the same slot
        checkedIn.CheckIn(Start);
        await SeedAsync(cancelled, checkedIn);

        var candidates = await ListCheckInCandidates(Start.AddMinutes(30));

        Assert.Equal([checkedIn.Id], candidates.Select(b => b.Id));
    }

    [Fact]
    public async Task ListWithOpenCheckInWindow_ReturnsTrackedBookingsThatCanBeCheckedInAndSaved()
    {
        await SeedRoomsAndUsersAsync();
        var booking = NewBooking(_room, _user);
        await SeedAsync(booking);
        var now = Start.AddMinutes(-5);

        await InScope(async sp =>
        {
            var candidates = await sp.GetRequiredService<IBookingRepository>()
                .ListWithOpenCheckInWindowAsync(_room.Id, _user.Id, now, Ct);
            candidates.Single().CheckIn(now);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var reloaded = await GetAsync(booking.Id);
        Assert.Equal(BookingStatus.CheckedIn, reloaded!.Status);
        Assert.Equal(now, reloaded.CheckedInAt);
    }

    // ---- overlap protection ----

    [Fact]
    public async Task Add_OverlappingBooking_ThrowsDataConflict()
    {
        await SeedRoomsAndUsersAsync();
        await AddAsync(NewBooking(_room, _user));

        var exception = await Assert.ThrowsAsync<DataConflictException>(
            () => AddAsync(NewBooking(_room, _otherUser, Start.AddMinutes(30), End.AddMinutes(30))));

        Assert.Equal("booking_overlap", exception.Code);
        Assert.Single(await ListByRoom(_room.Id));
    }

    [Fact]
    public async Task Add_BackToBackBookings_AreAllowed()
    {
        await SeedRoomsAndUsersAsync();
        await AddAsync(NewBooking(_room, _user));

        await AddAsync(NewBooking(_room, _user, End, End.AddHours(1)));
        await AddAsync(NewBooking(_room, _user, Start.AddHours(-1), Start));

        Assert.Equal(3, (await ListByRoom(_room.Id)).Count);
    }

    [Fact]
    public async Task Add_SameSlotInAnotherRoom_IsAllowed()
    {
        await SeedRoomsAndUsersAsync();
        await AddAsync(NewBooking(_room, _user));

        await AddAsync(NewBooking(_otherRoom, _otherUser));

        Assert.Single(await ListByRoom(_otherRoom.Id));
    }

    [Fact]
    public async Task Add_OverCancelledBooking_IsAllowed()
    {
        await SeedRoomsAndUsersAsync();
        var cancelled = NewBooking(_room, _user);
        cancelled.Cancel();
        await SeedAsync(cancelled);

        await AddAsync(NewBooking(_room, _otherUser));

        Assert.Equal(2, (await ListByRoom(_room.Id)).Count);
    }

    [Fact]
    public async Task CancellingABooking_FreesItsSlot()
    {
        await SeedRoomsAndUsersAsync();
        var booking = NewBooking(_room, _user);
        await SeedAsync(booking);

        await InScope(async sp =>
        {
            var loaded = await sp.GetRequiredService<IBookingRepository>().GetByIdAsync(booking.Id, Ct);
            loaded!.Cancel();
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });
        await AddAsync(NewBooking(_room, _otherUser));

        Assert.Equal(2, (await ListByRoom(_room.Id)).Count);
    }

    [Fact]
    public async Task ConcurrentRequestsForTheSameSlot_OnlyOneWins()
    {
        await SeedRoomsAndUsersAsync();

        // Request A has checked the slot and is about to save when request B books an overlapping slot first.
        await using var requestA = Provider.CreateAsyncScope();
        requestA.ServiceProvider.GetRequiredService<IBookingRepository>().Add(NewBooking(_room, _user));
        await AddAsync(NewBooking(_room, _otherUser, Start.AddMinutes(30), End.AddMinutes(30)));

        var exception = await Assert.ThrowsAsync<DataConflictException>(
            () => requestA.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));

        Assert.Equal("booking_overlap", exception.Code);
        Assert.Single(await ListByRoom(_room.Id));
    }

    [Fact]
    public async Task Add_BookingForUnknownRoomAndUser_FailsWithoutBeingReportedAsAConflict()
    {
        var orphan = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), Start, End);

        await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(orphan));
    }
}
