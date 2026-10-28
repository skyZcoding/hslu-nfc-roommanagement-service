using Microsoft.EntityFrameworkCore;
using RoomManagement.Application.Rooms;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(AppDbContext db) : IRoomRepository
{
    public Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Rooms.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Room?> GetByReaderIdAsync(ReaderId readerId, CancellationToken cancellationToken) =>
        db.Rooms.FirstOrDefaultAsync(r => r.ReaderId == readerId, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludingRoomId, CancellationToken cancellationToken)
    {
        // Matches the unique index on lower(name).
        var lowered = normalizedName.ToLowerInvariant();
        var query = db.Rooms.Where(r => r.Name.ToLower() == lowered);

        if (excludingRoomId is { } excluded)
            query = query.Where(r => r.Id != excluded);

        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Room>> ListAsync(string? building, bool activeOnly, CancellationToken cancellationToken)
    {
        var query = db.Rooms.AsNoTracking();

        if (building is not null)
        {
            var lowered = building.ToLowerInvariant();
            query = query.Where(r => r.Building.ToLower() == lowered);
        }

        if (activeOnly)
            query = query.Where(r => r.IsActive);

        return await query.OrderBy(r => r.Building).ThenBy(r => r.Floor).ThenBy(r => r.Name).ToListAsync(cancellationToken);
    }

    public void Add(Room room) => db.Rooms.Add(room);
}
