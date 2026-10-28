using Microsoft.EntityFrameworkCore;
using Npgsql;
using RoomManagement.Application.Common;

namespace RoomManagement.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation,
        } postgresException)
        {
            throw ToConflict(postgresException, ex);
        }
    }

    private static DataConflictException ToConflict(PostgresException postgresException, Exception inner) =>
        postgresException.ConstraintName switch
        {
            DbConstraints.UserEmailUnique => new("user_email_taken", "A user with this email already exists.", inner),
            DbConstraints.RoomNameUnique => new("room_name_taken", "A room with this name already exists.", inner),
            DbConstraints.RoomReaderUnique => new("reader_in_use", "This reader is already assigned to another room.", inner),
            DbConstraints.BookingNoOverlap => new("booking_overlap", "The room is already booked during this time.", inner),
            _ => new("data_conflict", "The change conflicts with existing data.", inner),
        };
}
