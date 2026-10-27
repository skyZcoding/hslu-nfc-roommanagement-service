using RoomManagement.Domain.Rooms;

namespace RoomManagement.Application.Rooms;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Room?> GetByReaderIdAsync(ReaderId readerId, CancellationToken cancellationToken);

    /// <summary>Room names are unique ignoring case.</summary>
    /// <param name="normalizedName">Name as returned by <see cref="Room.NormalizeName"/>.</param>
    /// <param name="excludingRoomId">A room to ignore, so a room does not clash with its own name when updated.</param>
    Task<bool> NameExistsAsync(string normalizedName, Guid? excludingRoomId, CancellationToken cancellationToken);

    /// <param name="building">Exact building name, compared ignoring case; null for all buildings.</param>
    /// <returns>The matching rooms ordered by building, floor and name.</returns>
    Task<IReadOnlyList<Room>> ListAsync(string? building, bool activeOnly, CancellationToken cancellationToken);

    void Add(Room room);
}
