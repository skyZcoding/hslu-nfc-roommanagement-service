namespace RoomManagement.Application.Common;

public interface IUnitOfWork
{
    /// <exception cref="DataConflictException">A uniqueness or overlap rule enforced by the database was violated.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
