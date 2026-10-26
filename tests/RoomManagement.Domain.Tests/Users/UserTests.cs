using RoomManagement.Domain.Common;
using RoomManagement.Domain.Users;

namespace RoomManagement.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static User CreateUser() => User.Create("jan@example.com", "Jan", UserRole.Member, Now);

    [Fact]
    public void Create_SetsPropertiesAndStartsActive()
    {
        var user = User.Create("jan@example.com", "Jan", UserRole.Admin, Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("jan@example.com", user.Email);
        Assert.Equal("Jan", user.DisplayName);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Create_NormalizesEmailAndDisplayName()
    {
        var user = User.Create("  Jan.Walker@Example.COM ", "  Jan Walker  ", UserRole.Member, Now);

        Assert.Equal("jan.walker@example.com", user.Email);
        Assert.Equal("Jan Walker", user.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    [InlineData("jan@")]
    [InlineData("jan@@example.com")]
    [InlineData("jan@example")]
    [InlineData("jan@.example.com")]
    [InlineData("jan@example.com.")]
    [InlineData("jan@exa..mple.com")]
    [InlineData("ja n@example.com")]
    public void Create_InvalidEmail_Throws(string? email)
    {
        Assert.Throws<DomainException>(() => User.Create(email!, "Jan", UserRole.Member, Now));
    }

    [Fact]
    public void Create_EmailTooLong_Throws()
    {
        var email = new string('a', User.MaxEmailLength) + "@example.com";

        Assert.Throws<DomainException>(() => User.Create(email, "Jan", UserRole.Member, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyDisplayName_Throws(string? displayName)
    {
        Assert.Throws<DomainException>(() => User.Create("jan@example.com", displayName!, UserRole.Member, Now));
    }

    [Fact]
    public void Create_DisplayNameTooLong_Throws()
    {
        var name = new string('x', User.MaxDisplayNameLength + 1);

        Assert.Throws<DomainException>(() => User.Create("jan@example.com", name, UserRole.Member, Now));
    }

    [Fact]
    public void Create_UndefinedRole_Throws()
    {
        Assert.Throws<DomainException>(() => User.Create("jan@example.com", "Jan", (UserRole)99, Now));
    }

    [Fact]
    public void Update_ChangesDisplayNameAndRole()
    {
        var user = CreateUser();

        user.Update("  Jan W.  ", UserRole.Admin);

        Assert.Equal("Jan W.", user.DisplayName);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void Update_InvalidDisplayName_Throws_AndKeepsState()
    {
        var user = CreateUser();

        Assert.Throws<DomainException>(() => user.Update(" ", UserRole.Admin));

        Assert.Equal("Jan", user.DisplayName);
        Assert.Equal(UserRole.Member, user.Role);
    }

    [Fact]
    public void Deactivate_ThenActivate_TogglesIsActive()
    {
        var user = CreateUser();

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();
        Assert.True(user.IsActive);
    }
}
