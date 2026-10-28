using RoomManagement.Api.Errors;
using RoomManagement.Application.Scans;

namespace RoomManagement.Api.Endpoints;

internal static class ReaderEndpoints
{
    public static RouteGroupBuilder MapReaderEndpoints(this RouteGroupBuilder api)
    {
        var readers = api.MapGroup("/readers").WithTags("Readers");

        readers.MapPost("/{readerId}/scans", async (
                    string readerId, CardScanRequest request, ProcessCardScanHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(readerId, request, ct)).ToHttpResult(scan => Results.Ok(scan)))
            .WithName("ProcessCardScan")
            .WithSummary("Called by a room's NFC reader when a card is held to it; the card carries the user id.")
            .WithDescription(
                "Checks the user in to their booking in the reader's room. The answer is always 200 for a known reader: " +
                "'granted' tells the reader to show green or red, and 'reason' says why a scan was denied. " +
                "A scan without a booking is denied; nothing is ever created. An unknown reader is a 404.")
            .Produces<CardScanResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
