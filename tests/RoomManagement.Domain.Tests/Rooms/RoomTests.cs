using RoomManagement.Domain.Common;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Domain.Tests.Rooms;

public class RoomTests
{
    private static Room CreateRoom() => Room.Create("H2.301", "Campus Rotkreuz", 3, 24);

    [Fact]
    public void Create_SetsPropertiesAndStartsActiveWithoutReader()
    {
        var room = Room.Create("H2.301", "Campus Rotkreuz", 3, 24);

        Assert.NotEqual(Guid.Empty, room.Id);
        Assert.Equal("H2.301", room.Name);
        Assert.Equal("Campus Rotkreuz", room.Building);
        Assert.Equal(3, room.Floor);
        Assert.Equal(24, room.Capacity);
        Assert.True(room.IsActive);
        Assert.Null(room.ReaderId);
    }

    [Fact]
    public void Create_TrimsNameAndBuilding()
    {
        var room = Room.Create("  H2.301 ", " Campus Rotkreuz  ", 0, 1);

        Assert.Equal("H2.301", room.Name);
        Assert.Equal("Campus Rotkreuz", room.Building);
    }

    [Fact]
    public void Create_NegativeFloor_IsAllowed()
    {
        Assert.Equal(-1, Room.Create("Archive", "Main", -1, 5).Floor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_EmptyName_Throws(string? name)
    {
        Assert.Throws<DomainException>(() => Room.Create(name!, "Main", 1, 5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_EmptyBuilding_Throws(string? building)
    {
        Assert.Throws<DomainException>(() => Room.Create("A1", building!, 1, 5));
    }

    [Fact]
    public void Create_NameOrBuildingTooLong_Throws()
    {
        Assert.Throws<DomainException>(() => Room.Create(new string('x', Room.MaxNameLength + 1), "Main", 1, 5));
        Assert.Throws<DomainException>(() => Room.Create("A1", new string('x', Room.MaxBuildingLength + 1), 1, 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_NonPositiveCapacity_Throws(int capacity)
    {
        Assert.Throws<DomainException>(() => Room.Create("A1", "Main", 1, capacity));
    }

    [Fact]
    public void Update_ChangesAllFields()
    {
        var room = CreateRoom();

        room.Update(" H2.302 ", "Main", 4, 30);

        Assert.Equal("H2.302", room.Name);
        Assert.Equal("Main", room.Building);
        Assert.Equal(4, room.Floor);
        Assert.Equal(30, room.Capacity);
    }

    [Fact]
    public void Update_Invalid_Throws_AndKeepsState()
    {
        var room = CreateRoom();

        Assert.Throws<DomainException>(() => room.Update("New name", "Main", 4, 0));

        Assert.Equal("H2.301", room.Name);
        Assert.Equal("Campus Rotkreuz", room.Building);
        Assert.Equal(3, room.Floor);
        Assert.Equal(24, room.Capacity);
    }

    [Fact]
    public void AssignReader_SetsReader_AndReplacesExisting()
    {
        var room = CreateRoom();

        room.AssignReader(ReaderId.Create("reader-1"));
        Assert.Equal(ReaderId.Create("reader-1"), room.ReaderId);

        room.AssignReader(ReaderId.Create("reader-2"));
        Assert.Equal(ReaderId.Create("reader-2"), room.ReaderId);
    }

    [Fact]
    public void AssignReader_Null_Throws()
    {
        Assert.Throws<DomainException>(() => CreateRoom().AssignReader(null!));
    }

    [Fact]
    public void RemoveReader_ClearsReader()
    {
        var room = CreateRoom();
        room.AssignReader(ReaderId.Create("reader-1"));

        room.RemoveReader();

        Assert.Null(room.ReaderId);
    }

    [Fact]
    public void Deactivate_ThenActivate_TogglesIsActive()
    {
        var room = CreateRoom();

        room.Deactivate();
        Assert.False(room.IsActive);

        room.Activate();
        Assert.True(room.IsActive);
    }
}
