using RoomManagement.Domain.Rooms;

namespace RoomManagement.Application.Rooms;

public sealed record CreateRoomRequest(string Name, string Building, int Floor, int Capacity);

public sealed record UpdateRoomRequest(string Name, string Building, int Floor, int Capacity);

public sealed record AssignReaderRequest(string ReaderId);

public sealed record ListRoomsQuery(string? Building = null, bool ActiveOnly = false);

public sealed record RoomDto(
    Guid Id, string Name, string Building, int Floor, int Capacity, string? ReaderId, bool IsActive)
{
    public static RoomDto From(Room room) =>
        new(room.Id, room.Name, room.Building, room.Floor, room.Capacity, room.ReaderId?.Value, room.IsActive);
}
