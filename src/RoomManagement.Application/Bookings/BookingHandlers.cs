using FluentValidation;
using RoomManagement.Application.Common;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Common;

namespace RoomManagement.Application.Bookings;

internal static class BookingErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("booking_not_found", $"Booking '{id}' was not found.");

    public static Error Overlap() =>
        Error.Conflict("booking_overlap", "The room is already booked during this time.");

    public static Error RoomInactive(Guid roomId) =>
        Error.Conflict("room_inactive", $"Room '{roomId}' is inactive and cannot be booked.");

    public static Error UserInactive(Guid userId) =>
        Error.Conflict("user_inactive", $"User '{userId}' is inactive and cannot book rooms.");

    public static Error UserDoesNotExist(Guid userId) => Error.Validation(new Dictionary<string, string[]>
    {
        [nameof(CreateBookingRequest.UserId)] = [$"User '{userId}' does not exist."],
    });

    public static Error InvalidState(string message) => Error.Conflict("booking_invalid_state", message);
}

internal static class BookingRepositoryExtensions
{
    /// <summary>True if no non-cancelled booking of the room overlaps [<paramref name="start"/>, <paramref name="end"/>).</summary>
    public static async Task<bool> IsSlotFreeAsync(
        this IBookingRepository bookings, Guid roomId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var inRange = await bookings.ListByRoomAsync(roomId, start, end, cancellationToken);
        return !inRange.Any(b => b.Overlaps(start, end));
    }
}

public sealed class CreateBookingHandler(
    IRoomRepository rooms,
    IUserRepository users,
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    IValidator<CreateBookingRequest> validator)
{
    public async Task<Result<BookingDto>> HandleAsync(
        Guid roomId, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(request, cancellationToken) is { } validationError)
            return validationError;

        var room = await rooms.GetByIdAsync(roomId, cancellationToken);
        if (room is null)
            return RoomErrors.NotFound(roomId);

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return BookingErrors.UserDoesNotExist(request.UserId);

        if (!room.IsActive)
            return BookingErrors.RoomInactive(room.Id);
        if (!user.IsActive)
            return BookingErrors.UserInactive(user.Id);

        if (!await bookings.IsSlotFreeAsync(room.Id, request.Start, request.End, cancellationToken))
            return BookingErrors.Overlap();

        var booking = Booking.Create(room.Id, user.Id, request.Start, request.End);
        bookings.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return BookingDto.From(booking);
    }
}

public sealed class GetBookingHandler(IBookingRepository bookings)
{
    public async Task<Result<BookingDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await bookings.GetByIdAsync(id, cancellationToken);
        return booking is null ? BookingErrors.NotFound(id) : BookingDto.From(booking);
    }
}

public sealed class CancelBookingHandler(IBookingRepository bookings, IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await bookings.GetByIdAsync(id, cancellationToken);
        if (booking is null)
            return BookingErrors.NotFound(id);

        try
        {
            booking.Cancel();
        }
        catch (InvalidStateTransitionException ex)
        {
            return BookingErrors.InvalidState(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Manual check-in by booking id (e.g. by an admin); card scans go through <c>ProcessCardScanHandler</c>.</summary>
public sealed class CheckInBookingHandler(IBookingRepository bookings, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await bookings.GetByIdAsync(id, cancellationToken);
        if (booking is null)
            return BookingErrors.NotFound(id);

        try
        {
            booking.CheckIn(clock.UtcNow);
        }
        catch (InvalidStateTransitionException ex)
        {
            return BookingErrors.InvalidState(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListRoomBookingsHandler(
    IRoomRepository rooms, IBookingRepository bookings, IValidator<ListBookingsQuery> validator)
{
    public async Task<Result<IReadOnlyList<BookingDto>>> HandleAsync(
        Guid roomId, ListBookingsQuery query, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(query, cancellationToken) is { } validationError)
            return validationError;

        if (await rooms.GetByIdAsync(roomId, cancellationToken) is null)
            return RoomErrors.NotFound(roomId);

        var result = await bookings.ListByRoomAsync(roomId, query.From, query.To, cancellationToken);
        // No implicit conversion here: C# does not allow user-defined conversions from interface types.
        return Result<IReadOnlyList<BookingDto>>.Success(BookingDto.FromAll(result));
    }
}

public sealed class ListUserBookingsHandler(
    IUserRepository users, IBookingRepository bookings, IValidator<ListBookingsQuery> validator)
{
    public async Task<Result<IReadOnlyList<BookingDto>>> HandleAsync(
        Guid userId, ListBookingsQuery query, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(query, cancellationToken) is { } validationError)
            return validationError;

        if (await users.GetByIdAsync(userId, cancellationToken) is null)
            return UserErrors.NotFound(userId);

        var result = await bookings.ListByUserAsync(userId, query.From, query.To, cancellationToken);
        return Result<IReadOnlyList<BookingDto>>.Success(BookingDto.FromAll(result));
    }
}

public sealed class GetRoomAvailabilityHandler(
    IRoomRepository rooms, IBookingRepository bookings, IValidator<AvailabilityQuery> validator)
{
    /// <summary>An inactive room is never available.</summary>
    public async Task<Result<RoomAvailabilityDto>> HandleAsync(
        Guid roomId, AvailabilityQuery query, CancellationToken cancellationToken)
    {
        if (await validator.ValidateToErrorAsync(query, cancellationToken) is { } validationError)
            return validationError;

        var room = await rooms.GetByIdAsync(roomId, cancellationToken);
        if (room is null)
            return RoomErrors.NotFound(roomId);

        var isAvailable = room.IsActive
            && await bookings.IsSlotFreeAsync(room.Id, query.From, query.To, cancellationToken);

        return new RoomAvailabilityDto(room.Id, query.From, query.To, isAvailable);
    }
}
