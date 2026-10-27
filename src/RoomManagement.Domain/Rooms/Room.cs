using RoomManagement.Domain.Common;

namespace RoomManagement.Domain.Rooms;

public class Room
{
    public const int MaxNameLength = 100;
    public const int MaxBuildingLength = 100;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Building { get; private set; }
    public int Floor { get; private set; }
    public int Capacity { get; private set; }
    public ReaderId? ReaderId { get; private set; }
    public bool IsActive { get; private set; }

    private Room(Guid id, string name, string building, int floor, int capacity)
    {
        Id = id;
        Name = name;
        Building = building;
        Floor = floor;
        Capacity = capacity;
        IsActive = true;
    }

    public static Room Create(string name, string building, int floor, int capacity) =>
        new(Guid.NewGuid(), NormalizeName(name), Normalize(building, "Building", MaxBuildingLength),
            floor, ValidateCapacity(capacity));

    public void Update(string name, string building, int floor, int capacity)
    {
        var newName = NormalizeName(name);
        var newBuilding = Normalize(building, "Building", MaxBuildingLength);
        var newCapacity = ValidateCapacity(capacity);

        Name = newName;
        Building = newBuilding;
        Floor = floor;
        Capacity = newCapacity;
    }

    public void AssignReader(ReaderId readerId) => ReaderId = readerId ?? throw new DomainException("Reader id is required.");

    public void RemoveReader() => ReaderId = null;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Trims the name; throws if it is empty or too long.</summary>
    public static string NormalizeName(string? name) => Normalize(name, "Name", MaxNameLength);

    private static string Normalize(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrEmpty(normalized))
            throw new DomainException($"{field} must not be empty.");
        if (normalized.Length > maxLength)
            throw new DomainException($"{field} must not be longer than {maxLength} characters.");

        return normalized;
    }

    private static int ValidateCapacity(int capacity) =>
        capacity > 0 ? capacity : throw new DomainException("Capacity must be greater than zero.");
}
