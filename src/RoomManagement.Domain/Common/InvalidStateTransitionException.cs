namespace RoomManagement.Domain.Common;

/// <summary>Raised when an operation is not allowed in the entity's current state (maps to HTTP 409).</summary>
public class InvalidStateTransitionException(string message) : DomainException(message);
