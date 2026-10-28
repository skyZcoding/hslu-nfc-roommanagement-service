using Microsoft.EntityFrameworkCore;
using RoomManagement.Application.Bookings;
using RoomManagement.Domain.Bookings;

namespace RoomManagement.Infrastructure.Persistence.Repositories;

internal sealed class BookingRepository(AppDbContext db) : IBookingRepository
{
    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Bookings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<IReadOnlyList<Booking>> ListByRoomAsync(
        Guid roomId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) =>
        ListOverlappingAsync(db.Bookings.Where(b => b.RoomId == roomId), from, to, cancellationToken);

    public Task<IReadOnlyList<Booking>> ListByUserAsync(
        Guid userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) =>
        ListOverlappingAsync(db.Bookings.Where(b => b.UserId == userId), from, to, cancellationToken);

    // Tracked on purpose: the card-scan handler checks one of these bookings in and saves it.
    public async Task<IReadOnlyList<Booking>> ListWithOpenCheckInWindowAsync(
        Guid roomId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Same condition as Booking.IsCheckInWindowOpen: Start - lead time <= now < End.
        var latestStart = now.ToUniversalTime() + Booking.CheckInLeadTime;
        var utcNow = now.ToUniversalTime();

        return await db.Bookings
            .Where(b => b.RoomId == roomId
                        && b.UserId == userId
                        && b.Status != BookingStatus.Cancelled
                        && b.Start <= latestStart
                        && b.End > utcNow)
            .OrderBy(b => b.Start)
            .ToListAsync(cancellationToken);
    }

    public void Add(Booking booking) => db.Bookings.Add(booking);

    private static async Task<IReadOnlyList<Booking>> ListOverlappingAsync(
        IQueryable<Booking> query, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        // Npgsql only accepts UTC values (offset 0) as timestamptz parameters.
        if (from?.ToUniversalTime() is { } utcFrom)
            query = query.Where(b => b.End > utcFrom);
        if (to?.ToUniversalTime() is { } utcTo)
            query = query.Where(b => b.Start < utcTo);

        return await query.AsNoTracking().OrderBy(b => b.Start).ThenBy(b => b.Id).ToListAsync(cancellationToken);
    }
}
