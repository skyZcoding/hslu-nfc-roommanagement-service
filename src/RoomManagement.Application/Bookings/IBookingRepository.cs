using RoomManagement.Domain.Bookings;

namespace RoomManagement.Application.Bookings;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Bookings of the room in any status that overlap the half-open range [<paramref name="from"/>, <paramref name="to"/>),
    /// ordered by start. A null bound is unbounded.
    /// </summary>
    Task<IReadOnlyList<Booking>> ListByRoomAsync(
        Guid roomId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);

    /// <summary>Same as <see cref="ListByRoomAsync"/>, for the bookings of one user across all rooms.</summary>
    Task<IReadOnlyList<Booking>> ListByUserAsync(
        Guid userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);

    /// <summary>
    /// The user's non-cancelled bookings in the room whose check-in window is open at <paramref name="now"/>
    /// (see <see cref="Booking.IsCheckInWindowOpen"/>), ordered by start.
    /// </summary>
    Task<IReadOnlyList<Booking>> ListWithOpenCheckInWindowAsync(
        Guid roomId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(Booking booking);
}
