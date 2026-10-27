using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using RoomManagement.Domain.Common;

namespace RoomManagement.Domain.Rooms;

/// <summary>Identifier of the NFC reader mounted at a room, e.g. <c>reader-h2-301</c>. Case-insensitive.</summary>
public sealed partial record ReaderId
{
    public const int MaxLength = 64;

    public string Value { get; }

    private ReaderId(string value) => Value = value;

    public static ReaderId Create(string? value) =>
        TryCreate(value, out var readerId, out var error) ? readerId : throw new DomainException(error);

    /// <summary>Like <see cref="Create"/>, but reports an invalid value through <paramref name="error"/> instead of throwing.</summary>
    public static bool TryCreate(
        string? value, [NotNullWhen(true)] out ReaderId? readerId, [NotNullWhen(false)] out string? error)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized))
            error = "Reader id must not be empty.";
        else if (normalized.Length > MaxLength)
            error = $"Reader id must not be longer than {MaxLength} characters.";
        else if (!AllowedCharacters().IsMatch(normalized))
            error = "Reader id may only contain letters, digits, '-' and '_'.";
        else
        {
            readerId = new ReaderId(normalized);
            error = null;
            return true;
        }

        readerId = null;
        return false;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9_-]+$")]
    private static partial Regex AllowedCharacters();
}
