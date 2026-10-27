using RoomManagement.Domain.Common;

namespace RoomManagement.Domain.Bookings;

public class Booking
{
    /// <summary>How long before <see cref="Start"/> a check-in is already accepted.</summary>
    public static readonly TimeSpan CheckInLeadTime = TimeSpan.FromMinutes(10);

    public Guid Id { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset End { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTimeOffset? CheckedInAt { get; private set; }

    private Booking(Guid id, Guid roomId, Guid userId, DateTimeOffset start, DateTimeOffset end)
    {
        Id = id;
        RoomId = roomId;
        UserId = userId;
        Start = start;
        End = end;
        Status = BookingStatus.Booked;
    }

    public static Booking Create(Guid roomId, Guid userId, DateTimeOffset start, DateTimeOffset end)
    {
        if (roomId == Guid.Empty)
            throw new DomainException("Room id is required.");
        if (userId == Guid.Empty)
            throw new DomainException("User id is required.");
        if (end <= start)
            throw new DomainException("End must be after start.");

        return new Booking(Guid.NewGuid(), roomId, userId, start.ToUniversalTime(), end.ToUniversalTime());
    }

    /// <summary>True while the booking would be accepted by a card scan at <paramref name="now"/>.</summary>
    public bool IsCheckInWindowOpen(DateTimeOffset now) => now >= Start - CheckInLeadTime && now < End;

    public void CheckIn(DateTimeOffset now)
    {
        if (Status != BookingStatus.Booked)
            throw new InvalidStateTransitionException($"A {Status} booking cannot be checked in.");
        if (!IsCheckInWindowOpen(now))
            throw new InvalidStateTransitionException("The check-in window for this booking is not open.");

        Status = BookingStatus.CheckedIn;
        CheckedInAt = now.ToUniversalTime();
    }

    public void Cancel()
    {
        if (Status != BookingStatus.Booked)
            throw new InvalidStateTransitionException($"A {Status} booking cannot be cancelled.");

        Status = BookingStatus.Cancelled;
    }

    /// <summary>
    /// True if this booking occupies the room during any part of [<paramref name="start"/>, <paramref name="end"/>).
    /// Cancelled bookings never block a time slot; back-to-back bookings do not overlap.
    /// </summary>
    public bool Overlaps(DateTimeOffset start, DateTimeOffset end) =>
        Status != BookingStatus.Cancelled && Start < end && start < End;
}
