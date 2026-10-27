using RoomManagement.Application.Common;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Tests.Fakes;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Application.Tests.Rooms;

public class RoomHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateRoomHandler CreateHandler() => new(_rooms, _unitOfWork, new CreateRoomRequestValidator());

    private UpdateRoomHandler UpdateHandler() => new(_rooms, _unitOfWork, new UpdateRoomRequestValidator());

    private AssignReaderHandler AssignHandler() => new(_rooms, _unitOfWork, new AssignReaderRequestValidator());

    private Room SeedRoom(
        string name = "H2.301", string building = "Campus Rotkreuz", int floor = 3, int capacity = 24,
        string? reader = null, bool active = true)
    {
        var room = Room.Create(name, building, floor, capacity);
        if (reader is not null) room.AssignReader(ReaderId.Create(reader));
        if (!active) room.Deactivate();
        _rooms.Add(room);
        return room;
    }

    // ---- create ----

    [Fact]
    public async Task Create_ValidRequest_PersistsAndReturnsDto()
    {
        var result = await CreateHandler().HandleAsync(new(" H2.301 ", "Campus Rotkreuz", 3, 24), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("H2.301", result.Value.Name);
        Assert.Equal("Campus Rotkreuz", result.Value.Building);
        Assert.Equal(3, result.Value.Floor);
        Assert.Equal(24, result.Value.Capacity);
        Assert.Null(result.Value.ReaderId);
        Assert.True(result.Value.IsActive);
        Assert.Single(_rooms.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsFieldErrors_AndPersistsNothing()
    {
        var result = await CreateHandler().HandleAsync(new("", " ", 1, 0), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        var fields = result.Error.ValidationErrors!.Keys;
        Assert.Contains(nameof(CreateRoomRequest.Name), fields);
        Assert.Contains(nameof(CreateRoomRequest.Building), fields);
        Assert.Contains(nameof(CreateRoomRequest.Capacity), fields);
        Assert.Empty(_rooms.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_DuplicateName_IgnoringCaseAndWhitespace_ReturnsConflict()
    {
        SeedRoom("H2.301");

        var result = await CreateHandler().HandleAsync(new("  h2.301 ", "Other building", 1, 10), Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("room_name_taken", result.Error.Code);
        Assert.Single(_rooms.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- get ----

    [Fact]
    public async Task Get_Existing_ReturnsDto()
    {
        var room = SeedRoom(reader: "reader-1");

        var result = await new GetRoomHandler(_rooms).HandleAsync(room.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(room.Id, result.Value.Id);
        Assert.Equal("reader-1", result.Value.ReaderId);
    }

    [Fact]
    public async Task Get_Unknown_ReturnsNotFound()
    {
        var result = await new GetRoomHandler(_rooms).HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("room_not_found", result.Error.Code);
    }

    // ---- get by reader ----

    [Fact]
    public async Task GetByReader_KnownReader_ReturnsItsRoom_IgnoringCaseAndWhitespace()
    {
        SeedRoom("A1", reader: "reader-2");
        var room = SeedRoom("H2.301", reader: "reader-1");

        var result = await new GetRoomByReaderHandler(_rooms).HandleAsync(" READER-1 ", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(room.Id, result.Value.Id);
    }

    [Theory]
    [InlineData("reader-9")]
    [InlineData("not a valid id!")]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetByReader_UnknownOrMalformedReader_ReturnsNotFound(string? readerId)
    {
        SeedRoom(reader: "reader-1");

        var result = await new GetRoomByReaderHandler(_rooms).HandleAsync(readerId, Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("reader_not_found", result.Error.Code);
    }

    // ---- list ----

    [Fact]
    public async Task List_NoFilter_ReturnsAllRooms()
    {
        SeedRoom("A1");
        SeedRoom("A2", active: false);

        var result = await new ListRoomsHandler(_rooms).HandleAsync(new ListRoomsQuery(), Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task List_ActiveOnly_ExcludesInactiveRooms()
    {
        SeedRoom("A1");
        SeedRoom("A2", active: false);

        var result = await new ListRoomsHandler(_rooms).HandleAsync(new ListRoomsQuery(ActiveOnly: true), Ct);

        Assert.Equal("A1", Assert.Single(result).Name);
    }

    [Fact]
    public async Task List_BuildingFilter_IsCaseInsensitiveAndTrimmed()
    {
        SeedRoom("A1", building: "Campus Rotkreuz");
        SeedRoom("B1", building: "Campus Luzern");

        var result = await new ListRoomsHandler(_rooms).HandleAsync(new ListRoomsQuery(Building: " campus rotkreuz "), Ct);

        Assert.Equal("A1", Assert.Single(result).Name);
    }

    [Fact]
    public async Task List_BlankBuilding_IsTreatedAsNoFilter()
    {
        SeedRoom("A1", building: "Campus Rotkreuz");
        SeedRoom("B1", building: "Campus Luzern");

        var result = await new ListRoomsHandler(_rooms).HandleAsync(new ListRoomsQuery(Building: "  "), Ct);

        Assert.Equal(2, result.Count);
    }

    // ---- update ----

    [Fact]
    public async Task Update_ValidRequest_ChangesRoomAndSaves()
    {
        var room = SeedRoom();

        var result = await UpdateHandler().HandleAsync(room.Id, new(" H2.302 ", "Main", 4, 30), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("H2.302", result.Value.Name);
        Assert.Equal("Main", room.Building);
        Assert.Equal(4, room.Floor);
        Assert.Equal(30, room.Capacity);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_KeepingOwnNameWithDifferentCase_Succeeds()
    {
        var room = SeedRoom("H2.301");

        var result = await UpdateHandler().HandleAsync(room.Id, new("h2.301", "Campus Rotkreuz", 3, 24), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("h2.301", room.Name);
    }

    [Fact]
    public async Task Update_NameOfAnotherRoom_ReturnsConflict_AndKeepsRoom()
    {
        SeedRoom("A1");
        var room = SeedRoom("A2");

        var result = await UpdateHandler().HandleAsync(room.Id, new("a1", "Campus Rotkreuz", 3, 24), Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("room_name_taken", result.Error.Code);
        Assert.Equal("A2", room.Name);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_InvalidRequest_ReturnsValidationError_AndKeepsRoom()
    {
        var room = SeedRoom();

        var result = await UpdateHandler().HandleAsync(room.Id, new("H2.302", "Main", 4, -1), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(nameof(UpdateRoomRequest.Capacity), result.Error.ValidationErrors!.Keys);
        Assert.Equal("H2.301", room.Name);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        var result = await UpdateHandler().HandleAsync(Guid.NewGuid(), new("H2.302", "Main", 4, 30), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- deactivate ----

    [Fact]
    public async Task Deactivate_Existing_DeactivatesAndSaves()
    {
        var room = SeedRoom();

        var result = await new DeactivateRoomHandler(_rooms, _unitOfWork).HandleAsync(room.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(room.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_IsIdempotent()
    {
        var room = SeedRoom(active: false);

        var result = await new DeactivateRoomHandler(_rooms, _unitOfWork).HandleAsync(room.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(room.IsActive);
    }

    [Fact]
    public async Task Deactivate_Unknown_ReturnsNotFound()
    {
        var result = await new DeactivateRoomHandler(_rooms, _unitOfWork).HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- assign reader ----

    [Fact]
    public async Task AssignReader_ValidRequest_SetsNormalizedReaderAndSaves()
    {
        var room = SeedRoom();

        var result = await AssignHandler().HandleAsync(room.Id, new(" Reader-H2_301 "), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReaderId.Create("reader-h2_301"), room.ReaderId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AssignReader_ReplacesTheRoomsExistingReader()
    {
        var room = SeedRoom(reader: "reader-old");

        var result = await AssignHandler().HandleAsync(room.Id, new("reader-new"), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReaderId.Create("reader-new"), room.ReaderId);
    }

    [Fact]
    public async Task AssignReader_SameReaderAgain_Succeeds()
    {
        var room = SeedRoom(reader: "reader-1");

        var result = await AssignHandler().HandleAsync(room.Id, new("READER-1"), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReaderId.Create("reader-1"), room.ReaderId);
    }

    [Fact]
    public async Task AssignReader_ReaderOfAnotherRoom_ReturnsConflict_AndChangesNothing()
    {
        var other = SeedRoom("A1", reader: "reader-1");
        var room = SeedRoom("A2");

        var result = await AssignHandler().HandleAsync(room.Id, new("reader-1"), Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("reader_in_use", result.Error.Code);
        Assert.Contains("A1", result.Error.Message);
        Assert.Null(room.ReaderId);
        Assert.Equal(ReaderId.Create("reader-1"), other.ReaderId);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    public async Task AssignReader_InvalidReaderId_ReturnsValidationError(string readerId)
    {
        var room = SeedRoom();

        var result = await AssignHandler().HandleAsync(room.Id, new(readerId), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(nameof(AssignReaderRequest.ReaderId), result.Error.ValidationErrors!.Keys);
        Assert.Null(room.ReaderId);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AssignReader_UnknownRoom_ReturnsNotFound()
    {
        var result = await AssignHandler().HandleAsync(Guid.NewGuid(), new("reader-1"), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- remove reader ----

    [Fact]
    public async Task RemoveReader_RoomWithReader_ClearsItAndSaves()
    {
        var room = SeedRoom(reader: "reader-1");

        var result = await new RemoveReaderHandler(_rooms, _unitOfWork).HandleAsync(room.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(room.ReaderId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RemoveReader_RoomWithoutReader_IsIdempotent()
    {
        var room = SeedRoom();

        var result = await new RemoveReaderHandler(_rooms, _unitOfWork).HandleAsync(room.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(room.ReaderId);
    }

    [Fact]
    public async Task RemoveReader_UnknownRoom_ReturnsNotFound()
    {
        var result = await new RemoveReaderHandler(_rooms, _unitOfWork).HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
