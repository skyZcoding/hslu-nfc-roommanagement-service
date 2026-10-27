using FluentValidation;
using RoomManagement.Application.Common;
using RoomManagement.Domain.Users;

namespace RoomManagement.Application.Users;

internal static class UserErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("user_not_found", $"User '{id}' was not found.");

    public static Error EmailTaken(string email) =>
        Error.Conflict("user_email_taken", $"A user with email '{email}' already exists.");
}

public sealed class CreateUserHandler(
    IUserRepository users, IUnitOfWork unitOfWork, IClock clock, IValidator<CreateUserRequest> validator)
{
    public async Task<Result<UserDto>> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken) is { } validationError)
            return validationError;

        var email = User.NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
            return UserErrors.EmailTaken(email);

        var user = User.Create(email, request.DisplayName, request.Role, clock.UtcNow);
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}

public sealed class GetUserHandler(IUserRepository users)
{
    public async Task<Result<UserDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null ? UserErrors.NotFound(id) : UserDto.From(user);
    }
}

public sealed class ListUsersHandler(IUserRepository users)
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var result = await users.ListAsync(query.ActiveOnly, query.Role, cancellationToken);
        return result.Select(UserDto.From).ToList();
    }
}

public sealed class UpdateUserHandler(
    IUserRepository users, IUnitOfWork unitOfWork, IValidator<UpdateUserRequest> validator)
{
    public async Task<Result<UserDto>> HandleAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken) is { } validationError)
            return validationError;

        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return UserErrors.NotFound(id);

        user.Update(request.DisplayName, request.Role);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}

public sealed class DeactivateUserHandler(IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return UserErrors.NotFound(id);

        user.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
