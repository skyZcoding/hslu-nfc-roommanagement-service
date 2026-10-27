using FluentValidation;
using RoomManagement.Application.Common;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Application.Rooms;

internal static class RoomErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("room_not_found", $"Room '{id}' was not found.");

    public static Error NameTaken(string name) =>
        Error.Conflict("room_name_taken", $"A room named '{name}' already exists.");

    public static Error ReaderNotFound(string? readerId) =>
        Error.NotFound("reader_not_found", $"No room has a reader '{readerId}'.");

    public static Error ReaderInUse(ReaderId readerId, Room room) =>
        Error.Conflict("reader_in_use", $"Reader '{readerId}' is already assigned to room '{room.Name}' ({room.Id}).");
}

public sealed class CreateRoomHandler(
    IRoomRepository rooms, IUnitOfWork unitOfWork, IValidator<CreateRoomRequest> validator)
{
    public async Task<Result<RoomDto>> HandleAsync(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken) is { } validationError)
            return validationError;

        var name = Room.NormalizeName(request.Name);
        if (await rooms.NameExistsAsync(name, null, cancellationToken))
            return RoomErrors.NameTaken(name);

        var room = Room.Create(name, request.Building, request.Floor, request.Capacity);
        rooms.Add(room);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RoomDto.From(room);
    }
}

public sealed class GetRoomHandler(IRoomRepository rooms)
{
    public async Task<Result<RoomDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetByIdAsync(id, cancellationToken);
        return room is null ? RoomErrors.NotFound(id) : RoomDto.From(room);
    }
}

public sealed class GetRoomByReaderHandler(IRoomRepository rooms)
{
    /// <summary>A malformed reader id cannot belong to any room, so it is reported as not found.</summary>
    public async Task<Result<RoomDto>> HandleAsync(string? readerId, CancellationToken cancellationToken)
    {
        if (!ReaderId.TryCreate(readerId, out var id, out _))
            return RoomErrors.ReaderNotFound(readerId);

        var room = await rooms.GetByReaderIdAsync(id, cancellationToken);
        return room is null ? RoomErrors.ReaderNotFound(readerId) : RoomDto.From(room);
    }
}

public sealed class ListRoomsHandler(IRoomRepository rooms)
{
    public async Task<IReadOnlyList<RoomDto>> HandleAsync(ListRoomsQuery query, CancellationToken cancellationToken)
    {
        var building = string.IsNullOrWhiteSpace(query.Building) ? null : query.Building.Trim();

        var result = await rooms.ListAsync(building, query.ActiveOnly, cancellationToken);
        return result.Select(RoomDto.From).ToList();
    }
}

public sealed class UpdateRoomHandler(
    IRoomRepository rooms, IUnitOfWork unitOfWork, IValidator<UpdateRoomRequest> validator)
{
    public async Task<Result<RoomDto>> HandleAsync(Guid id, UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken) is { } validationError)
            return validationError;

        var room = await rooms.GetByIdAsync(id, cancellationToken);
        if (room is null)
            return RoomErrors.NotFound(id);

        var name = Room.NormalizeName(request.Name);
        if (await rooms.NameExistsAsync(name, room.Id, cancellationToken))
            return RoomErrors.NameTaken(name);

        room.Update(name, request.Building, request.Floor, request.Capacity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RoomDto.From(room);
    }
}

public sealed class DeactivateRoomHandler(IRoomRepository rooms, IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetByIdAsync(id, cancellationToken);
        if (room is null)
            return RoomErrors.NotFound(id);

        room.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class AssignReaderHandler(
    IRoomRepository rooms, IUnitOfWork unitOfWork, IValidator<AssignReaderRequest> validator)
{
    public async Task<Result> HandleAsync(Guid roomId, AssignReaderRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken) is { } validationError)
            return validationError;

        var room = await rooms.GetByIdAsync(roomId, cancellationToken);
        if (room is null)
            return RoomErrors.NotFound(roomId);

        var readerId = ReaderId.Create(request.ReaderId);
        var current = await rooms.GetByReaderIdAsync(readerId, cancellationToken);
        if (current is not null && current.Id != room.Id)
            return RoomErrors.ReaderInUse(readerId, current);

        room.AssignReader(readerId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class RemoveReaderHandler(IRoomRepository rooms, IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await rooms.GetByIdAsync(roomId, cancellationToken);
        if (room is null)
            return RoomErrors.NotFound(roomId);

        room.RemoveReader();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
