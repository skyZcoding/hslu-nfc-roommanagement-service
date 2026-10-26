using RoomManagement.Domain.Common;

namespace RoomManagement.Domain.Users;

public class User
{
    public const int MaxEmailLength = 254;
    public const int MaxDisplayNameLength = 100;

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User(Guid id, string email, string displayName, UserRole role, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        Role = role;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public static User Create(string email, string displayName, UserRole role, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), NormalizeEmail(email), NormalizeDisplayName(displayName), ValidateRole(role), createdAt);

    public void Update(string displayName, UserRole role)
    {
        DisplayName = NormalizeDisplayName(displayName);
        Role = ValidateRole(role);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Trims and lowercases the email; throws if it is not a plausible address.</summary>
    public static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized))
            throw new DomainException("Email must not be empty.");
        if (normalized.Length > MaxEmailLength)
            throw new DomainException($"Email must not be longer than {MaxEmailLength} characters.");

        var at = normalized.IndexOf('@');
        var valid = at > 0
            && at == normalized.LastIndexOf('@')
            && !normalized.Any(char.IsWhiteSpace)
            && IsValidDomain(normalized[(at + 1)..]);

        return valid ? normalized : throw new DomainException($"'{email}' is not a valid email address.");
    }

    private static bool IsValidDomain(string domain) =>
        domain.Contains('.')
        && !domain.StartsWith('.')
        && !domain.EndsWith('.')
        && !domain.Contains("..");

    private static string NormalizeDisplayName(string? displayName)
    {
        var normalized = displayName?.Trim();

        if (string.IsNullOrEmpty(normalized))
            throw new DomainException("Display name must not be empty.");
        if (normalized.Length > MaxDisplayNameLength)
            throw new DomainException($"Display name must not be longer than {MaxDisplayNameLength} characters.");

        return normalized;
    }

    private static UserRole ValidateRole(UserRole role) =>
        Enum.IsDefined(role) ? role : throw new DomainException($"'{role}' is not a valid role.");
}
