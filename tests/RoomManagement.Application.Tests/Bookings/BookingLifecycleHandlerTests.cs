using RoomManagement.Application.Bookings;
using RoomManagement.Application.Common;
using RoomManagement.Domain.Bookings;

namespace RoomManagement.Application.Tests.Bookings;

public class BookingLifecycleHandlerTests : BookingTestBase
{
    private CancelBookingHandler CancelHandler() => new(Bookings, UnitOfWork);

    private CheckInBookingHandler CheckInHandler() => new(Bookings, UnitOfWork, Clock);

    private Booking SeedDefaultBooking() => SeedBooking(SeedRoom(), SeedUser());

    // ---- get ----

    [Fact]
    public async Task Get_Existing_ReturnsDto()
    {
        var booking = SeedDefaultBooking();

        var result = await new GetBookingHandler(Bookings).HandleAsync(booking.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(booking.Id, result.Value.Id);
        Assert.Equal(BookingStatus.Booked, result.Value.Status);
    }

    [Fact]
    public async Task Get_Unknown_ReturnsNotFound()
    {
        var result = await new GetBookingHandler(Bookings).HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("booking_not_found", result.Error.Code);
    }

    // ---- cancel ----

    [Fact]
    public async Task Cancel_BookedBooking_CancelsAndSaves()
    {
        var booking = SeedDefaultBooking();

        var result = await CancelHandler().HandleAsync(booking.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsConflict()
    {
        var booking = SeedDefaultBooking();
        booking.Cancel();

        var result = await CancelHandler().HandleAsync(booking.Id, Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("booking_invalid_state", result.Error.Code);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Cancel_CheckedInBooking_ReturnsConflict_AndKeepsStatus()
    {
        var booking = SeedDefaultBooking();
        booking.CheckIn(Start);

        var result = await CancelHandler().HandleAsync(booking.Id, Ct);

        Assert.Equal("booking_invalid_state", result.Error!.Code);
        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Cancel_Unknown_ReturnsNotFound()
    {
        var result = await CancelHandler().HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    // ---- manual check-in ----

    [Fact]
    public async Task CheckIn_InsideWindow_ChecksInWithCurrentTime()
    {
        var booking = SeedDefaultBooking();
        Clock.UtcNow = Start.AddMinutes(-5);

        var result = await CheckInHandler().HandleAsync(booking.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
        Assert.Equal(Clock.UtcNow, booking.CheckedInAt);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(-11)] // window not open yet
    [InlineData(60)] // booking is over
    public async Task CheckIn_OutsideWindow_ReturnsConflict(int minutesFromStart)
    {
        var booking = SeedDefaultBooking();
        Clock.UtcNow = Start.AddMinutes(minutesFromStart);

        var result = await CheckInHandler().HandleAsync(booking.Id, Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("booking_invalid_state", result.Error.Code);
        Assert.Equal(BookingStatus.Booked, booking.Status);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CheckIn_CancelledOrAlreadyCheckedIn_ReturnsConflict()
    {
        var cancelled = SeedDefaultBooking();
        cancelled.Cancel();
        var checkedIn = SeedBooking(SeedRoom("H2.302", reader: "reader-2"), SeedUser("other@example.com"));
        checkedIn.CheckIn(Start);

        var cancelledResult = await CheckInHandler().HandleAsync(cancelled.Id, Ct);
        var checkedInResult = await CheckInHandler().HandleAsync(checkedIn.Id, Ct);

        Assert.Equal("booking_invalid_state", cancelledResult.Error!.Code);
        Assert.Equal("booking_invalid_state", checkedInResult.Error!.Code);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CheckIn_Unknown_ReturnsNotFound()
    {
        var result = await CheckInHandler().HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }
}
