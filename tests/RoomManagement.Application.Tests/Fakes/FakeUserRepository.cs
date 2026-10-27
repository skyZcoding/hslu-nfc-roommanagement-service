using RoomManagement.Application.Users;
using RoomManagement.Domain.Users;

namespace RoomManagement.Application.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    public List<User> Items { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(u => u.Email == normalizedEmail));

    public Task<IReadOnlyList<User>> ListAsync(bool activeOnly, UserRole? role, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<User>>(Items
            .Where(u => (!activeOnly || u.IsActive) && (role is null || u.Role == role))
            .ToList());

    public void Add(User user) => Items.Add(user);
}
