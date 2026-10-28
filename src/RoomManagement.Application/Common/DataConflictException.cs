namespace RoomManagement.Application.Common;

/// <summary>
/// Thrown by <see cref="IUnitOfWork"/> when the database rejects a change because it violates a uniqueness or
/// overlap rule. Handlers check these rules up front, so this is what a lost race against a concurrent request
/// looks like; the API reports it as a conflict.
/// </summary>
/// <param name="code">Machine-readable code, using the same codes as the handlers' conflict errors.</param>
public sealed class DataConflictException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
