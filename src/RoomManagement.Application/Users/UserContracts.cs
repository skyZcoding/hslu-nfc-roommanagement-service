using RoomManagement.Domain.Users;

namespace RoomManagement.Application.Users;

public sealed record CreateUserRequest(string Email, string DisplayName, UserRole Role);

public sealed record UpdateUserRequest(string DisplayName, UserRole Role);

public sealed record ListUsersQuery(bool ActiveOnly = false, UserRole? Role = null);

public sealed record UserDto(Guid Id, string Email, string DisplayName, UserRole Role, bool IsActive, DateTimeOffset CreatedAt)
{
    public static UserDto From(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt);
}
