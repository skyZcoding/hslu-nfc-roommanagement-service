namespace RoomManagement.Application.Scans;

/// <summary>Sent by a room's NFC reader; <paramref name="UserId"/> is the payload read from the user's card.</summary>
public sealed record CardScanRequest(Guid UserId);

public enum ScanDenialReason
{
    NoActiveBooking,
    UserInactive,
    UserUnknown,
    AlreadyCheckedIn,
    RoomInactive,
}

/// <summary>
/// Outcome of a card scan, shaped for the reader to show green or red.
/// <see cref="UserDisplayName"/> is set whenever the user is known.
/// </summary>
public sealed record CardScanResult(
    bool Granted,
    ScanDenialReason? Reason,
    Guid RoomId,
    Guid? BookingId,
    string? UserDisplayName)
{
    public static CardScanResult Grant(Guid roomId, Guid bookingId, string userDisplayName) =>
        new(true, null, roomId, bookingId, userDisplayName);

    public static CardScanResult Deny(ScanDenialReason reason, Guid roomId, string? userDisplayName = null) =>
        new(false, reason, roomId, null, userDisplayName);
}
