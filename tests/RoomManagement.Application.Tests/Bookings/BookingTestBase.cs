using RoomManagement.Application.Tests.Fakes;
using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Rooms;
using RoomManagement.Domain.Users;

namespace RoomManagement.Application.Tests.Bookings;

/// <summary>Shared fakes and seed helpers; xUnit creates a fresh instance for every test.</summary>
public abstract class BookingTestBase
{
    protected static readonly CancellationToken Ct = CancellationToken.None;
    protected static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    protected static readonly DateTimeOffset End = Start.AddHours(1);

    protected FakeRoomRepository Rooms { get; } = new();
    protected FakeUserRepository Users { get; } = new();
    protected FakeBookingRepository Bookings { get; } = new();
    protected FakeUnitOfWork UnitOfWork { get; } = new();
    protected FakeClock Clock { get; } = new(Start);

    protected Room SeedRoom(string name = "H2.301", string? reader = "reader-1", bool active = true)
    {
        var room = Room.Create(name, "Campus Rotkreuz", 3, 24);
        if (reader is not null) room.AssignReader(ReaderId.Create(reader));
        if (!active) room.Deactivate();
        Rooms.Add(room);
        return room;
    }

    protected User SeedUser(string email = "jan@example.com", string displayName = "Jan", bool active = true)
    {
        var user = User.Create(email, displayName, UserRole.Member, Start);
        if (!active) user.Deactivate();
        Users.Add(user);
        return user;
    }

    /// <summary>Seeds a <see cref="BookingStatus.Booked"/> booking; defaults to [<see cref="Start"/>, <see cref="End"/>).</summary>
    protected Booking SeedBooking(Room room, User user, DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        var booking = Booking.Create(room.Id, user.Id, start ?? Start, end ?? End);
        Bookings.Add(booking);
        return booking;
    }
}
