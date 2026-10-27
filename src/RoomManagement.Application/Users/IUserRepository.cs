using RoomManagement.Domain.Users;

namespace RoomManagement.Application.Users;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <param name="normalizedEmail">Email as returned by <see cref="User.NormalizeEmail"/>.</param>
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListAsync(bool activeOnly, UserRole? role, CancellationToken cancellationToken);

    void Add(User user);
}
