using RoomManagement.Application.Rooms;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Application.Tests.Fakes;

public sealed class FakeRoomRepository : IRoomRepository
{
    public List<Room> Items { get; } = [];

    public Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

    public Task<Room?> GetByReaderIdAsync(ReaderId readerId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(r => r.ReaderId == readerId));

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludingRoomId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(r =>
            r.Id != excludingRoomId && string.Equals(r.Name, normalizedName, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Room>> ListAsync(string? building, bool activeOnly, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Room>>(Items
            .Where(r => (!activeOnly || r.IsActive)
                        && (building is null || string.Equals(r.Building, building, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(r => r.Building).ThenBy(r => r.Floor).ThenBy(r => r.Name)
            .ToList());

    public void Add(Room room) => Items.Add(room);
}
