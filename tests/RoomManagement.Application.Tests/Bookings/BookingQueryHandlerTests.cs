using RoomManagement.Application.Bookings;
using RoomManagement.Application.Common;
using RoomManagement.Domain.Bookings;

namespace RoomManagement.Application.Tests.Bookings;

public class BookingQueryHandlerTests : BookingTestBase
{
    private static readonly DateTimeOffset Later = Start.AddDays(1);

    private ListRoomBookingsHandler ListByRoom() => new(Rooms, Bookings, new ListBookingsQueryValidator());

    private ListUserBookingsHandler ListByUser() => new(Users, Bookings, new ListBookingsQueryValidator());

    private GetRoomAvailabilityHandler Availability() => new(Rooms, Bookings, new AvailabilityQueryValidator());

    // ---- list by room ----

    [Fact]
    public async Task ListByRoom_ReturnsAllBookingsOfTheRoomOrderedByStart_IncludingCancelled()
    {
        var room = SeedRoom();
        var otherRoom = SeedRoom("H2.302", reader: "reader-2");
        var user = SeedUser();
        var second = SeedBooking(room, user, Later, Later.AddHours(1));
        var first = SeedBooking(room, user);
        first.Cancel();
        SeedBooking(otherRoom, user);

        var result = await ListByRoom().HandleAsync(room.Id, new ListBookingsQuery(), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([first.Id, second.Id], result.Value.Select(b => b.Id));
        Assert.Equal(BookingStatus.Cancelled, result.Value[0].Status);
    }

    [Fact]
    public async Task ListByRoom_WithRange_ReturnsOnlyOverlappingBookings()
    {
        var room = SeedRoom();
        var user = SeedUser();
        var today = SeedBooking(room, user);
        SeedBooking(room, user, Later, Later.AddHours(1));

        var result = await ListByRoom().HandleAsync(room.Id, new ListBookingsQuery(Start.AddMinutes(30), Start.AddHours(2)), Ct);

        Assert.Equal(today.Id, Assert.Single(result.Value).Id);
    }

    [Fact]
    public async Task ListByRoom_RangeStartingExactlyWhenABookingEnds_DoesNotIncludeIt()
    {
        var room = SeedRoom();
        SeedBooking(room, SeedUser());

        var result = await ListByRoom().HandleAsync(room.Id, new ListBookingsQuery(From: End), Ct);

        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListByRoom_RoomWithoutBookings_ReturnsEmptyList()
    {
        var room = SeedRoom();

        var result = await ListByRoom().HandleAsync(room.Id, new ListBookingsQuery(), Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListByRoom_UnknownRoom_ReturnsNotFound()
    {
        var result = await ListByRoom().HandleAsync(Guid.NewGuid(), new ListBookingsQuery(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("room_not_found", result.Error.Code);
    }

    [Fact]
    public async Task ListByRoom_InvalidRange_ReturnsValidationError()
    {
        var room = SeedRoom();

        var result = await ListByRoom().HandleAsync(room.Id, new ListBookingsQuery(End, Start), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(nameof(ListBookingsQuery.To), result.Error.ValidationErrors!.Keys);
    }

    // ---- list by user ----

    [Fact]
    public async Task ListByUser_ReturnsOnlyThatUsersBookingsAcrossRooms()
    {
        var room = SeedRoom();
        var otherRoom = SeedRoom("H2.302", reader: "reader-2");
        var jan = SeedUser("jan@example.com");
        var anna = SeedUser("anna@example.com", "Anna");
        var inRoom = SeedBooking(room, jan);
        var inOtherRoom = SeedBooking(otherRoom, jan, Later, Later.AddHours(1));
        SeedBooking(room, anna, Later, Later.AddHours(1));

        var result = await ListByUser().HandleAsync(jan.Id, new ListBookingsQuery(), Ct);

        Assert.Equal([inRoom.Id, inOtherRoom.Id], result.Value.Select(b => b.Id));
    }

    [Fact]
    public async Task ListByUser_WithRange_ReturnsOnlyOverlappingBookings()
    {
        var room = SeedRoom();
        var user = SeedUser();
        SeedBooking(room, user);
        var tomorrow = SeedBooking(room, user, Later, Later.AddHours(1));

        var result = await ListByUser().HandleAsync(user.Id, new ListBookingsQuery(From: Later), Ct);

        Assert.Equal(tomorrow.Id, Assert.Single(result.Value).Id);
    }

    [Fact]
    public async Task ListByUser_UnknownUser_ReturnsNotFound()
    {
        var result = await ListByUser().HandleAsync(Guid.NewGuid(), new ListBookingsQuery(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("user_not_found", result.Error.Code);
    }

    [Fact]
    public async Task ListByUser_InvalidRange_ReturnsValidationError()
    {
        var user = SeedUser();

        var result = await ListByUser().HandleAsync(user.Id, new ListBookingsQuery(End, Start), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    // ---- availability ----

    [Fact]
    public async Task Availability_FreeSlot_IsAvailable()
    {
        var room = SeedRoom();

        var result = await Availability().HandleAsync(room.Id, new(Start, End), Ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsAvailable);
        Assert.Equal(room.Id, result.Value.RoomId);
        Assert.Equal(Start, result.Value.From);
        Assert.Equal(End, result.Value.To);
    }

    [Fact]
    public async Task Availability_OverlappingBooking_IsNotAvailable()
    {
        var room = SeedRoom();
        SeedBooking(room, SeedUser());

        var result = await Availability().HandleAsync(room.Id, new(Start.AddMinutes(30), End.AddMinutes(30)), Ct);

        Assert.False(result.Value.IsAvailable);
    }

    [Fact]
    public async Task Availability_SlotRightAfterABooking_IsAvailable()
    {
        var room = SeedRoom();
        SeedBooking(room, SeedUser());

        var result = await Availability().HandleAsync(room.Id, new(End, End.AddHours(1)), Ct);

        Assert.True(result.Value.IsAvailable);
    }

    [Fact]
    public async Task Availability_OnlyCancelledBookingInSlot_IsAvailable()
    {
        var room = SeedRoom();
        SeedBooking(room, SeedUser()).Cancel();

        var result = await Availability().HandleAsync(room.Id, new(Start, End), Ct);

        Assert.True(result.Value.IsAvailable);
    }

    [Fact]
    public async Task Availability_InactiveRoom_IsNeverAvailable()
    {
        var room = SeedRoom(active: false);

        var result = await Availability().HandleAsync(room.Id, new(Start, End), Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsAvailable);
    }

    [Fact]
    public async Task Availability_UnknownRoom_ReturnsNotFound()
    {
        var result = await Availability().HandleAsync(Guid.NewGuid(), new(Start, End), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Availability_InvalidRange_ReturnsValidationError()
    {
        var room = SeedRoom();

        var result = await Availability().HandleAsync(room.Id, new(End, Start), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(nameof(AvailabilityQuery.To), result.Error.ValidationErrors!.Keys);
    }
}
