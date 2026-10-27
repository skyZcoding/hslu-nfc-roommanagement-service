using RoomManagement.Application.Bookings;
using RoomManagement.Domain.Bookings;

namespace RoomManagement.Application.Tests.Fakes;

public sealed class FakeBookingRepository : IBookingRepository
{
    public List<Booking> Items { get; } = [];

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(b => b.Id == id));

    public Task<IReadOnlyList<Booking>> ListByRoomAsync(
        Guid roomId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) =>
        Task.FromResult(Query(b => b.RoomId == roomId && InRange(b, from, to)));

    public Task<IReadOnlyList<Booking>> ListByUserAsync(
        Guid userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) =>
        Task.FromResult(Query(b => b.UserId == userId && InRange(b, from, to)));

    public Task<IReadOnlyList<Booking>> ListWithOpenCheckInWindowAsync(
        Guid roomId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Query(b => b.RoomId == roomId
                                   && b.UserId == userId
                                   && b.Status != BookingStatus.Cancelled
                                   && b.IsCheckInWindowOpen(now)));

    public void Add(Booking booking) => Items.Add(booking);

    private IReadOnlyList<Booking> Query(Func<Booking, bool> predicate) =>
        Items.Where(predicate).OrderBy(b => b.Start).ToList();

    private static bool InRange(Booking booking, DateTimeOffset? from, DateTimeOffset? to) =>
        (to is null || booking.Start < to) && (from is null || booking.End > from);
}
