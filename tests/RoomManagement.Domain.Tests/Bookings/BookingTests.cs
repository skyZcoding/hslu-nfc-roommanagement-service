using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Common;

namespace RoomManagement.Domain.Tests.Bookings;

public class BookingTests
{
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(1);

    private static Booking CreateBooking() => Booking.Create(RoomId, UserId, Start, End);

    [Fact]
    public void Create_SetsPropertiesAndStartsBooked()
    {
        var booking = CreateBooking();

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(RoomId, booking.RoomId);
        Assert.Equal(UserId, booking.UserId);
        Assert.Equal(Start, booking.Start);
        Assert.Equal(End, booking.End);
        Assert.Equal(BookingStatus.Booked, booking.Status);
        Assert.Null(booking.CheckedInAt);
    }

    [Fact]
    public void Create_ConvertsTimesToUtc()
    {
        var plusTwo = TimeSpan.FromHours(2);

        var booking = Booking.Create(RoomId, UserId, new DateTimeOffset(2026, 11, 2, 11, 0, 0, plusTwo),
            new DateTimeOffset(2026, 11, 2, 12, 0, 0, plusTwo));

        Assert.Equal(TimeSpan.Zero, booking.Start.Offset);
        Assert.Equal(TimeSpan.Zero, booking.End.Offset);
        Assert.Equal(Start, booking.Start);
        Assert.Equal(End, booking.End);
    }

    [Fact]
    public void Create_EmptyIds_Throw()
    {
        Assert.Throws<DomainException>(() => Booking.Create(Guid.Empty, UserId, Start, End));
        Assert.Throws<DomainException>(() => Booking.Create(RoomId, Guid.Empty, Start, End));
    }

    [Fact]
    public void Create_EndNotAfterStart_Throws()
    {
        Assert.Throws<DomainException>(() => Booking.Create(RoomId, UserId, Start, Start));
        Assert.Throws<DomainException>(() => Booking.Create(RoomId, UserId, Start, Start.AddMinutes(-1)));
    }

    [Theory]
    [InlineData(-11, false)]
    [InlineData(-10, true)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(61, false)]
    public void IsCheckInWindowOpen_UsesTenMinutesLeadAndExclusiveEnd(int minutesFromStart, bool expected)
    {
        var booking = CreateBooking();

        Assert.Equal(expected, booking.IsCheckInWindowOpen(Start.AddMinutes(minutesFromStart)));
    }

    [Fact]
    public void CheckIn_InWindow_SetsStatusAndTimestamp()
    {
        var booking = CreateBooking();
        var now = Start.AddMinutes(-5);

        booking.CheckIn(now);

        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
        Assert.Equal(now, booking.CheckedInAt);
    }

    [Fact]
    public void CheckIn_OutsideWindow_Throws_AndKeepsState()
    {
        var booking = CreateBooking();

        Assert.Throws<InvalidStateTransitionException>(() => booking.CheckIn(Start.AddMinutes(-11)));
        Assert.Throws<InvalidStateTransitionException>(() => booking.CheckIn(End));

        Assert.Equal(BookingStatus.Booked, booking.Status);
        Assert.Null(booking.CheckedInAt);
    }

    [Fact]
    public void CheckIn_Twice_Throws()
    {
        var booking = CreateBooking();
        booking.CheckIn(Start);

        Assert.Throws<InvalidStateTransitionException>(() => booking.CheckIn(Start.AddMinutes(1)));
    }

    [Fact]
    public void CheckIn_Cancelled_Throws()
    {
        var booking = CreateBooking();
        booking.Cancel();

        Assert.Throws<InvalidStateTransitionException>(() => booking.CheckIn(Start));
    }

    [Fact]
    public void Cancel_Booked_SetsCancelled()
    {
        var booking = CreateBooking();

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void Cancel_AlreadyCancelledOrCheckedIn_Throws()
    {
        var cancelled = CreateBooking();
        cancelled.Cancel();
        Assert.Throws<InvalidStateTransitionException>(cancelled.Cancel);

        var checkedIn = CreateBooking();
        checkedIn.CheckIn(Start);
        Assert.Throws<InvalidStateTransitionException>(checkedIn.Cancel);
    }

    [Theory]
    [InlineData(-30, 30, true)] // overlaps the beginning
    [InlineData(30, 90, true)] // overlaps the end
    [InlineData(10, 20, true)] // contained
    [InlineData(-30, 120, true)] // contains
    [InlineData(0, 60, true)] // identical
    [InlineData(-60, 0, false)] // ends exactly at start
    [InlineData(60, 120, false)] // starts exactly at end
    [InlineData(-120, -60, false)] // before
    [InlineData(120, 180, false)] // after
    public void Overlaps_DetectsIntersectionOfHalfOpenRanges(int fromMinutes, int toMinutes, bool expected)
    {
        var booking = CreateBooking();

        Assert.Equal(expected, booking.Overlaps(Start.AddMinutes(fromMinutes), Start.AddMinutes(toMinutes)));
    }

    [Fact]
    public void Overlaps_CancelledBooking_NeverBlocks()
    {
        var booking = CreateBooking();
        booking.Cancel();

        Assert.False(booking.Overlaps(Start, End));
    }

    [Fact]
    public void Overlaps_CheckedInBooking_StillBlocks()
    {
        var booking = CreateBooking();
        booking.CheckIn(Start);

        Assert.True(booking.Overlaps(Start, End));
    }
}
