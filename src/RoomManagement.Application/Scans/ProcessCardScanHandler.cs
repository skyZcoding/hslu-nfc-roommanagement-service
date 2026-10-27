using RoomManagement.Application.Bookings;
using RoomManagement.Application.Common;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Application.Scans;

/// <summary>
/// Handles a user's card being held to a room's reader: the room is resolved from the reader and the user's
/// booking in that room is checked in. Without a matching booking the scan is denied; nothing is ever created.
/// </summary>
public sealed class ProcessCardScanHandler(
    IRoomRepository rooms, IUserRepository users, IBookingRepository bookings, IUnitOfWork unitOfWork, IClock clock)
{
    /// <returns>
    /// A failed result (not found) if no room has this reader, otherwise a granted or denied <see cref="CardScanResult"/>.
    /// </returns>
    public async Task<Result<CardScanResult>> HandleAsync(
        string? readerId, CardScanRequest request, CancellationToken cancellationToken)
    {
        if (!ReaderId.TryCreate(readerId, out var id, out _))
            return RoomErrors.ReaderNotFound(readerId);

        var room = await rooms.GetByReaderIdAsync(id, cancellationToken);
        if (room is null)
            return RoomErrors.ReaderNotFound(readerId);
        if (!room.IsActive)
            return CardScanResult.Deny(ScanDenialReason.RoomInactive, room.Id);

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return CardScanResult.Deny(ScanDenialReason.UserUnknown, room.Id);
        if (!user.IsActive)
            return CardScanResult.Deny(ScanDenialReason.UserInactive, room.Id, user.DisplayName);

        var now = clock.UtcNow;
        var candidates = await bookings.ListWithOpenCheckInWindowAsync(room.Id, user.Id, now, cancellationToken);

        // With back-to-back bookings several windows can be open at once: the earliest unused booking wins.
        var toCheckIn = candidates.Where(b => b.Status == BookingStatus.Booked).MinBy(b => b.Start);
        if (toCheckIn is not null)
        {
            toCheckIn.CheckIn(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return CardScanResult.Grant(room.Id, toCheckIn.Id, user.DisplayName);
        }

        var reason = candidates.Any(b => b.Status == BookingStatus.CheckedIn)
            ? ScanDenialReason.AlreadyCheckedIn
            : ScanDenialReason.NoActiveBooking;

        return CardScanResult.Deny(reason, room.Id, user.DisplayName);
    }
}
