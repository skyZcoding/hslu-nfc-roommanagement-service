using Microsoft.EntityFrameworkCore;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Users;

namespace RoomManagement.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(bool activeOnly, UserRole? role, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();

        if (activeOnly)
            query = query.Where(u => u.IsActive);
        if (role is { } wanted)
            query = query.Where(u => u.Role == wanted);

        return await query.OrderBy(u => u.DisplayName).ThenBy(u => u.Email).ToListAsync(cancellationToken);
    }

    public void Add(User user) => db.Users.Add(user);
}
