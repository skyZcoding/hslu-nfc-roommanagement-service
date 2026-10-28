using RoomManagement.Api.Errors;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Users;

namespace RoomManagement.Api.Endpoints;

internal static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users").WithTags("Users");

        users.MapGet("/", async (bool? activeOnly, UserRole? role, ListUsersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new ListUsersQuery(activeOnly ?? false, role), ct)))
            .WithName("ListUsers")
            .WithSummary("Lists users, ordered by display name; optionally only active ones and/or one role.")
            .Produces<IReadOnlyList<UserDto>>();

        users.MapGet("/{id:guid}", async (Guid id, GetUserHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(user => Results.Ok(user)))
            .WithName("GetUser")
            .WithSummary("Gets one user.")
            .Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPost("/", async (CreateUserRequest request, CreateUserHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct))
                .ToHttpResult(user => Results.CreatedAtRoute("GetUser", new { id = user.Id }, user)))
            .WithName("CreateUser")
            .WithSummary("Creates a user. The email must be unique (ignoring case).")
            .Produces<UserDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UpdateUserHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToHttpResult(user => Results.Ok(user)))
            .WithName("UpdateUser")
            .WithSummary("Replaces a user's display name and role.")
            .Produces<UserDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapDelete("/{id:guid}", async (Guid id, DeactivateUserHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(() => Results.NoContent()))
            .WithName("DeactivateUser")
            .WithSummary("Deactivates a user. The user and their bookings are kept.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
