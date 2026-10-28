using RoomManagement.Api.Errors;
using RoomManagement.Application.Bookings;

namespace RoomManagement.Api.Endpoints;

internal static class BookingEndpoints
{
    public static RouteGroupBuilder MapBookingEndpoints(this RouteGroupBuilder api)
    {
        var ofRoom = api.MapGroup("/rooms/{roomId:guid}").WithTags("Bookings");

        ofRoom.MapGet("/bookings", async (
                    Guid roomId, DateTimeOffset? from, DateTimeOffset? to, ListRoomBookingsHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(roomId, new ListBookingsQuery(from, to), ct)).ToHttpResult(list => Results.Ok(list)))
            .WithName("ListRoomBookings")
            .WithSummary("Lists the room's bookings in any status, ordered by start; optionally only those overlapping from–to.")
            .Produces<IReadOnlyList<BookingDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        ofRoom.MapPost("/bookings", async (
                    Guid roomId, CreateBookingRequest request, CreateBookingHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(roomId, request, ct))
                .ToHttpResult(booking => Results.CreatedAtRoute("GetBooking", new { id = booking.Id }, booking)))
            .WithName("CreateBooking")
            .WithSummary("Books the room for a user. Rooms cannot be double-booked; back-to-back bookings are fine.")
            .Produces<BookingDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        ofRoom.MapGet("/availability", async (
                    Guid roomId, DateTimeOffset from, DateTimeOffset to, GetRoomAvailabilityHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(roomId, new AvailabilityQuery(from, to), ct)).ToHttpResult(a => Results.Ok(a)))
            .WithName("GetRoomAvailability")
            .WithSummary("Tells whether the room is free between from and to. An inactive room is never available.")
            .Produces<RoomAvailabilityDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var bookings = api.MapGroup("/bookings").WithTags("Bookings");

        bookings.MapGet("/{id:guid}", async (Guid id, GetBookingHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(booking => Results.Ok(booking)))
            .WithName("GetBooking")
            .WithSummary("Gets one booking.")
            .Produces<BookingDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        bookings.MapPost("/{id:guid}/cancel", async (Guid id, CancelBookingHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(() => Results.NoContent()))
            .WithName("CancelBooking")
            .WithSummary("Cancels a booking that has not been checked in. The slot becomes free again.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        bookings.MapPost("/{id:guid}/check-in", async (Guid id, CheckInBookingHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(() => Results.NoContent()))
            .WithName("CheckInBooking")
            .WithSummary("Checks a booking in by hand (e.g. by an admin), from 10 minutes before it starts until it ends. Users normally check in with their card.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var ofUser = api.MapGroup("/users/{userId:guid}").WithTags("Bookings");

        ofUser.MapGet("/bookings", async (
                    Guid userId, DateTimeOffset? from, DateTimeOffset? to, ListUserBookingsHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(userId, new ListBookingsQuery(from, to), ct)).ToHttpResult(list => Results.Ok(list)))
            .WithName("ListUserBookings")
            .WithSummary("Lists the user's bookings in any room and status, ordered by start; optionally only those overlapping from–to.")
            .Produces<IReadOnlyList<BookingDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
