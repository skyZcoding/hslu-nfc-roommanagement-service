using RoomManagement.Domain.Common;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Domain.Tests.Rooms;

public class ReaderIdTests
{
    [Fact]
    public void Create_TrimsAndLowercases()
    {
        var id = ReaderId.Create("  Reader-H2_301 ");

        Assert.Equal("reader-h2_301", id.Value);
        Assert.Equal("reader-h2_301", id.ToString());
    }

    [Fact]
    public void Equality_IsCaseInsensitiveBecauseOfNormalization()
    {
        Assert.Equal(ReaderId.Create("READER-1"), ReaderId.Create("reader-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("reader/1")]
    [InlineData("reader.1")]
    [InlineData("rëader")]
    public void Create_Invalid_Throws(string? value)
    {
        Assert.Throws<DomainException>(() => ReaderId.Create(value));
    }

    [Fact]
    public void Create_AtMaxLength_Succeeds_AndAboveThrows()
    {
        Assert.Equal(ReaderId.MaxLength, ReaderId.Create(new string('a', ReaderId.MaxLength)).Value.Length);
        Assert.Throws<DomainException>(() => ReaderId.Create(new string('a', ReaderId.MaxLength + 1)));
    }
}
