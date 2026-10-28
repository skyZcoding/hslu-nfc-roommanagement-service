namespace RoomManagement.Infrastructure.Persistence;

/// <summary>Names of the database constraints that back the uniqueness and overlap rules.</summary>
internal static class DbConstraints
{
    public const string UserEmailUnique = "ix_users_email";
    public const string RoomNameUnique = "ix_rooms_name_lower";
    public const string RoomReaderUnique = "ix_rooms_reader_id";
    public const string BookingNoOverlap = "ex_bookings_room_no_overlap";
}
