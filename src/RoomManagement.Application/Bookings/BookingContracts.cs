using RoomManagement.Domain.Bookings;

namespace RoomManagement.Application.Bookings;

public sealed record CreateBookingRequest(Guid UserId, DateTimeOffset Start, DateTimeOffset End);

/// <summary>Optional time window: bookings overlapping it are returned. A null bound is unbounded.</summary>
public sealed record ListBookingsQuery(DateTimeOffset? From = null, DateTimeOffset? To = null);

public sealed record AvailabilityQuery(DateTimeOffset From, DateTimeOffset To);

public sealed record BookingDto(
    Guid Id,
    Guid RoomId,
    Guid UserId,
    DateTimeOffset Start,
    DateTimeOffset End,
    BookingStatus Status,
    DateTimeOffset? CheckedInAt)
{
    public static BookingDto From(Booking booking) => new(
        booking.Id, booking.RoomId, booking.UserId, booking.Start, booking.End, booking.Status, booking.CheckedInAt);

    public static IReadOnlyList<BookingDto> FromAll(IEnumerable<Booking> bookings) =>
        bookings.Select(From).ToList();
}

public sealed record RoomAvailabilityDto(Guid RoomId, DateTimeOffset From, DateTimeOffset To, bool IsAvailable);
