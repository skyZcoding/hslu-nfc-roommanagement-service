using RoomManagement.Application.Rooms;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Application.Tests.Rooms;

public class RoomValidatorTests
{
    private readonly CreateRoomRequestValidator _create = new();
    private readonly UpdateRoomRequestValidator _update = new();
    private readonly AssignReaderRequestValidator _assign = new();

    [Fact]
    public void Create_ValidRequest_IsValid_AlsoWithNegativeFloor()
    {
        Assert.True(_create.Validate(new CreateRoomRequest("H2.301", "Campus Rotkreuz", 3, 24)).IsValid);
        Assert.True(_create.Validate(new CreateRoomRequest("Archive", "Campus Rotkreuz", -1, 5)).IsValid);
    }

    [Theory]
    [InlineData("", "Campus")]
    [InlineData("   ", "Campus")]
    [InlineData("H2.301", "")]
    [InlineData("H2.301", "   ")]
    public void Create_EmptyNameOrBuilding_IsInvalid(string name, string building)
    {
        Assert.False(_create.Validate(new CreateRoomRequest(name, building, 1, 5)).IsValid);
    }

    [Fact]
    public void Create_TooLongNameAndBuilding_AreReportedPerField()
    {
        var result = _create.Validate(new CreateRoomRequest(
            new string('x', Room.MaxNameLength + 1), new string('y', Room.MaxBuildingLength + 1), 1, 5));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRoomRequest.Name));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRoomRequest.Building));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveCapacity_IsInvalid(int capacity)
    {
        var result = _create.Validate(new CreateRoomRequest("H2.301", "Campus", 1, capacity));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateRoomRequest.Capacity));
    }

    [Fact]
    public void Update_ValidRequest_IsValid()
    {
        Assert.True(_update.Validate(new UpdateRoomRequest("H2.302", "Campus", 4, 30)).IsValid);
    }

    [Fact]
    public void Update_InvalidFields_AreReportedPerField()
    {
        var result = _update.Validate(new UpdateRoomRequest("", "", 1, 0));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateRoomRequest.Name));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateRoomRequest.Building));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateRoomRequest.Capacity));
    }

    [Fact]
    public void AssignReader_ValidReaderId_IsValid()
    {
        Assert.True(_assign.Validate(new AssignReaderRequest("reader-h2_301")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("reader/1")]
    public void AssignReader_InvalidReaderId_IsReportedWithTheDomainMessage(string readerId)
    {
        var result = _assign.Validate(new AssignReaderRequest(readerId));

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(AssignReaderRequest.ReaderId), error.PropertyName);
        Assert.StartsWith("Reader id", error.ErrorMessage);
    }
}
