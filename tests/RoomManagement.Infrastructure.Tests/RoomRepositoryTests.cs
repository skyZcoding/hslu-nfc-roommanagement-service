using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Application.Common;
using RoomManagement.Application.Rooms;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public class RoomRepositoryTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private Task<Room?> GetAsync(Guid id) => InScope(sp => sp.GetRequiredService<IRoomRepository>().GetByIdAsync(id, Ct));

    private Task AddAsync(Room room) => InScope(async sp =>
    {
        sp.GetRequiredService<IRoomRepository>().Add(room);
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    });

    [Fact]
    public async Task Add_ThenGetById_RoundTripsAllFieldsIncludingTheReader()
    {
        var room = Room.Create("H2.301", "Campus Rotkreuz", -1, 24);
        room.AssignReader(ReaderId.Create("reader-h2-301"));

        await AddAsync(room);
        var loaded = await GetAsync(room.Id);

        Assert.NotNull(loaded);
        Assert.Equal("H2.301", loaded.Name);
        Assert.Equal("Campus Rotkreuz", loaded.Building);
        Assert.Equal(-1, loaded.Floor);
        Assert.Equal(24, loaded.Capacity);
        Assert.Equal(ReaderId.Create("reader-h2-301"), loaded.ReaderId);
        Assert.True(loaded.IsActive);
    }

    [Fact]
    public async Task RoomWithoutReader_IsStoredWithNullReader()
    {
        var room = NewRoom();

        await AddAsync(room);

        Assert.Null((await GetAsync(room.Id))!.ReaderId);
    }

    [Fact]
    public async Task ChangesToALoadedRoom_ArePersistedBySaveChanges()
    {
        var room = NewRoom(reader: "reader-1");
        await SeedAsync(room);

        await InScope(async sp =>
        {
            var loaded = await sp.GetRequiredService<IRoomRepository>().GetByIdAsync(room.Id, Ct);
            loaded!.Update("H2.302", "Main", 4, 30);
            loaded.RemoveReader();
            loaded.Deactivate();
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var reloaded = await GetAsync(room.Id);
        Assert.Equal("H2.302", reloaded!.Name);
        Assert.Equal("Main", reloaded.Building);
        Assert.Equal(30, reloaded.Capacity);
        Assert.Null(reloaded.ReaderId);
        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public async Task GetByReaderId_FindsTheRoomWithThatReader()
    {
        var other = NewRoom("A1", reader: "reader-2");
        var room = NewRoom("H2.301", reader: "reader-1");
        await SeedAsync(other, room);

        var found = await InScope(sp => sp.GetRequiredService<IRoomRepository>().GetByReaderIdAsync(ReaderId.Create(" READER-1 "), Ct));
        var missing = await InScope(sp => sp.GetRequiredService<IRoomRepository>().GetByReaderIdAsync(ReaderId.Create("reader-9"), Ct));

        Assert.Equal(room.Id, found!.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task NameExists_IgnoresCase_AndCanExcludeTheRoomItself()
    {
        var room = NewRoom("H2.301");
        await SeedAsync(room);

        Task<bool> Exists(string name, Guid? excluding) =>
            InScope(sp => sp.GetRequiredService<IRoomRepository>().NameExistsAsync(name, excluding, Ct));

        Assert.True(await Exists("H2.301", null));
        Assert.True(await Exists("h2.301", null));
        Assert.False(await Exists("H2.302", null));
        Assert.False(await Exists("h2.301", room.Id));
        Assert.True(await Exists("H2.301", Guid.NewGuid()));
    }

    [Fact]
    public async Task List_FiltersByBuildingIgnoringCaseAndActive_OrderedByBuildingFloorName()
    {
        var luzern = NewRoom("L1", "Campus Luzern", 1);
        var floor2 = NewRoom("B2", "Campus Rotkreuz", 2);
        var floor1B = NewRoom("B1", "Campus Rotkreuz", 1);
        var floor1A = NewRoom("A1", "Campus Rotkreuz", 1);
        var inactive = NewRoom("Z9", "Campus Rotkreuz", 1);
        inactive.Deactivate();
        await SeedAsync(luzern, floor2, floor1B, floor1A, inactive);

        Task<IReadOnlyList<Room>> List(string? building, bool activeOnly) =>
            InScope(sp => sp.GetRequiredService<IRoomRepository>().ListAsync(building, activeOnly, Ct));

        Assert.Equal(["L1", "A1", "B1", "Z9", "B2"], (await List(null, false)).Select(r => r.Name));
        Assert.Equal(["L1", "A1", "B1", "B2"], (await List(null, true)).Select(r => r.Name));
        Assert.Equal(["A1", "B1", "Z9", "B2"], (await List("campus rotkreuz", false)).Select(r => r.Name));
        Assert.Equal(["A1", "B1", "B2"], (await List("CAMPUS ROTKREUZ", true)).Select(r => r.Name));
        Assert.Empty(await List("Nowhere", false));
    }

    [Fact]
    public async Task Add_DuplicateNameDifferingOnlyInCase_ThrowsDataConflict()
    {
        await SeedAsync(NewRoom("H2.301"));

        var exception = await Assert.ThrowsAsync<DataConflictException>(() => AddAsync(NewRoom("h2.301")));

        Assert.Equal("room_name_taken", exception.Code);
    }

    [Fact]
    public async Task Add_ReaderAlreadyUsedByAnotherRoom_ThrowsDataConflict()
    {
        await SeedAsync(NewRoom("A1", reader: "reader-1"));

        var exception = await Assert.ThrowsAsync<DataConflictException>(() => AddAsync(NewRoom("A2", reader: "reader-1")));

        Assert.Equal("reader_in_use", exception.Code);
    }

    [Fact]
    public async Task ManyRoomsWithoutReader_AreAllowed()
    {
        await AddAsync(NewRoom("A1"));
        await AddAsync(NewRoom("A2"));

        var rooms = await InScope(sp => sp.GetRequiredService<IRoomRepository>().ListAsync(null, false, Ct));
        Assert.Equal(2, rooms.Count);
    }
}
