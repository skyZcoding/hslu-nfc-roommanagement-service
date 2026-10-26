namespace RoomManagement.Domain.Common;

/// <summary>Raised when a domain rule is violated.</summary>
public class DomainException(string message) : Exception(message);
