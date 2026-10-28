using RoomManagement.Api.Errors;
using RoomManagement.Application.Rooms;

namespace RoomManagement.Api.Endpoints;

internal static class RoomEndpoints
{
    public static RouteGroupBuilder MapRoomEndpoints(this RouteGroupBuilder api)
    {
        var rooms = api.MapGroup("/rooms").WithTags("Rooms");

        rooms.MapGet("/", async (string? building, bool? activeOnly, ListRoomsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new ListRoomsQuery(building, activeOnly ?? false), ct)))
            .WithName("ListRooms")
            .WithSummary("Lists rooms ordered by building, floor and name; optionally one building (ignoring case) and/or only active rooms.")
            .Produces<IReadOnlyList<RoomDto>>();

        rooms.MapGet("/{id:guid}", async (Guid id, GetRoomHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(room => Results.Ok(room)))
            .WithName("GetRoom")
            .WithSummary("Gets one room.")
            .Produces<RoomDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        rooms.MapGet("/by-reader/{readerId}", async (string readerId, GetRoomByReaderHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(readerId, ct)).ToHttpResult(room => Results.Ok(room)))
            .WithName("GetRoomByReader")
            .WithSummary("Finds the room that an NFC reader is mounted in (the reader id is not case-sensitive).")
            .Produces<RoomDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        rooms.MapPost("/", async (CreateRoomRequest request, CreateRoomHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct))
                .ToHttpResult(room => Results.CreatedAtRoute("GetRoom", new { id = room.Id }, room)))
            .WithName("CreateRoom")
            .WithSummary("Creates a room without a reader. The name must be unique (ignoring case).")
            .Produces<RoomDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        rooms.MapPut("/{id:guid}", async (Guid id, UpdateRoomRequest request, UpdateRoomHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToHttpResult(room => Results.Ok(room)))
            .WithName("UpdateRoom")
            .WithSummary("Replaces a room's name, building, floor and capacity. Its reader and active state are kept.")
            .Produces<RoomDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        rooms.MapPut("/{id:guid}/reader", async (Guid id, AssignReaderRequest request, AssignReaderHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToHttpResult(() => Results.NoContent()))
            .WithName("AssignReader")
            .WithSummary("Assigns an NFC reader to the room, replacing its current one. A reader belongs to at most one room.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        rooms.MapDelete("/{id:guid}/reader", async (Guid id, RemoveReaderHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(() => Results.NoContent()))
            .WithName("RemoveReader")
            .WithSummary("Removes the room's NFC reader. Nobody can check in to the room until it has a reader again.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        rooms.MapDelete("/{id:guid}", async (Guid id, DeactivateRoomHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(() => Results.NoContent()))
            .WithName("DeactivateRoom")
            .WithSummary("Deactivates a room. The room, its reader and its bookings are kept; it can no longer be booked.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
