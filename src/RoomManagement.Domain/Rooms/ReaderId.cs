using System.Text.RegularExpressions;
using RoomManagement.Domain.Common;

namespace RoomManagement.Domain.Rooms;

/// <summary>Identifier of the NFC reader mounted at a room, e.g. <c>reader-h2-301</c>. Case-insensitive.</summary>
public sealed partial record ReaderId
{
    public const int MaxLength = 64;

    public string Value { get; }

    private ReaderId(string value) => Value = value;

    public static ReaderId Create(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized))
            throw new DomainException("Reader id must not be empty.");
        if (normalized.Length > MaxLength)
            throw new DomainException($"Reader id must not be longer than {MaxLength} characters.");
        if (!AllowedCharacters().IsMatch(normalized))
            throw new DomainException("Reader id may only contain letters, digits, '-' and '_'.");

        return new ReaderId(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9_-]+$")]
    private static partial Regex AllowedCharacters();
}
