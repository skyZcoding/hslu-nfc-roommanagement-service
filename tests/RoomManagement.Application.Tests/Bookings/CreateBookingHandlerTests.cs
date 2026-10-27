using RoomManagement.Application.Bookings;
using RoomManagement.Application.Common;
using RoomManagement.Domain.Bookings;

namespace RoomManagement.Application.Tests.Bookings;

public class CreateBookingHandlerTests : BookingTestBase
{
    private CreateBookingHandler Handler() =>
        new(Rooms, Users, Bookings, UnitOfWork, new CreateBookingRequestValidator());

    [Fact]
    public async Task ValidRequest_PersistsBookedBookingAndReturnsDto()
    {
        var room = SeedRoom();
        var user = SeedUser();

        var result = await Handler().HandleAsync(room.Id, new(user.Id, Start, End), Ct);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(room.Id, dto.RoomId);
        Assert.Equal(user.Id, dto.UserId);
        Assert.Equal(Start, dto.Start);
        Assert.Equal(End, dto.End);
        Assert.Equal(BookingStatus.Booked, dto.Status);
        Assert.Null(dto.CheckedInAt);
        Assert.Equal(dto.Id, Assert.Single(Bookings.Items).Id);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task InvalidRequest_ReturnsFieldErrors_AndPersistsNothing()
    {
        var room = SeedRoom();

        var result = await Handler().HandleAsync(room.Id, new(Guid.Empty, End, Start), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        var fields = result.Error.ValidationErrors!.Keys;
        Assert.Contains(nameof(CreateBookingRequest.UserId), fields);
        Assert.Contains(nameof(CreateBookingRequest.End), fields);
        Assert.Empty(Bookings.Items);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UnknownRoom_ReturnsNotFound()
    {
        var user = SeedUser();

        var result = await Handler().HandleAsync(Guid.NewGuid(), new(user.Id, Start, End), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("room_not_found", result.Error.Code);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UnknownUser_ReturnsValidationErrorOnUserId()
    {
        var room = SeedRoom();

        var result = await Handler().HandleAsync(room.Id, new(Guid.NewGuid(), Start, End), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(nameof(CreateBookingRequest.UserId), result.Error.ValidationErrors!.Keys);
        Assert.Empty(Bookings.Items);
    }

    [Fact]
    public async Task InactiveRoom_ReturnsConflict()
    {
        var room = SeedRoom(active: false);
        var user = SeedUser();

        var result = await Handler().HandleAsync(room.Id, new(user.Id, Start, End), Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("room_inactive", result.Error.Code);
        Assert.Empty(Bookings.Items);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task InactiveUser_ReturnsConflict()
    {
        var room = SeedRoom();
        var user = SeedUser(active: false);

        var result = await Handler().HandleAsync(room.Id, new(user.Id, Start, End), Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("user_inactive", result.Error.Code);
        Assert.Empty(Bookings.Items);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(-30, 30)] // overlaps the beginning of the existing booking
    [InlineData(0, 60)] // identical
    [InlineData(15, 45)] // contained
    [InlineData(30, 90)] // overlaps the end
    public async Task OverlapWithExistingBooking_ReturnsConflict(int fromMinutes, int toMinutes)
    {
        var room = SeedRoom();
        var user = SeedUser();
        SeedBooking(room, user);

        var result = await Handler().HandleAsync(
            room.Id, new(user.Id, Start.AddMinutes(fromMinutes), Start.AddMinutes(toMinutes)), Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("booking_overlap", result.Error.Code);
        Assert.Single(Bookings.Items);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(-60, 0)] // directly before
    [InlineData(60, 120)] // directly after
    public async Task BackToBackWithExistingBooking_Succeeds(int fromMinutes, int toMinutes)
    {
        var room = SeedRoom();
        var user = SeedUser();
        SeedBooking(room, user);

        var result = await Handler().HandleAsync(
            room.Id, new(user.Id, Start.AddMinutes(fromMinutes), Start.AddMinutes(toMinutes)), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Bookings.Items.Count);
    }

    [Fact]
    public async Task SlotOfCancelledBooking_CanBeBookedAgain()
    {
        var room = SeedRoom();
        var user = SeedUser();
        SeedBooking(room, user).Cancel();

        var result = await Handler().HandleAsync(room.Id, new(user.Id, Start, End), Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SameSlotInAnotherRoom_Succeeds()
    {
        var room = SeedRoom("H2.301", reader: "reader-1");
        var otherRoom = SeedRoom("H2.302", reader: "reader-2");
        var user = SeedUser();
        SeedBooking(otherRoom, user);

        var result = await Handler().HandleAsync(room.Id, new(user.Id, Start, End), Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TimesWithOffset_AreComparedAsInstantsAndStoredAsUtc()
    {
        var room = SeedRoom();
        var user = SeedUser();
        SeedBooking(room, user); // 09:00-10:00 UTC
        var plusTwo = TimeSpan.FromHours(2);

        // 11:30-12:30 +02:00 is 09:30-10:30 UTC and therefore overlaps.
        var overlapping = await Handler().HandleAsync(
            room.Id, new(user.Id, new DateTimeOffset(2026, 11, 2, 11, 30, 0, plusTwo), new DateTimeOffset(2026, 11, 2, 12, 30, 0, plusTwo)), Ct);
        // 12:00-13:00 +02:00 is 10:00-11:00 UTC and is free.
        var free = await Handler().HandleAsync(
            room.Id, new(user.Id, new DateTimeOffset(2026, 11, 2, 12, 0, 0, plusTwo), new DateTimeOffset(2026, 11, 2, 13, 0, 0, plusTwo)), Ct);

        Assert.Equal("booking_overlap", overlapping.Error!.Code);
        Assert.True(free.IsSuccess);
        Assert.Equal(TimeSpan.Zero, free.Value.Start.Offset);
        Assert.Equal(End, free.Value.Start);
    }
}
