using RoomManagement.Application.Users;
using RoomManagement.Domain.Users;

namespace RoomManagement.Application.Tests.Users;

public class UserValidatorTests
{
    private readonly CreateUserRequestValidator _create = new();
    private readonly UpdateUserRequestValidator _update = new();

    [Fact]
    public void Create_ValidRequest_IsValid()
    {
        Assert.True(_create.Validate(new CreateUserRequest("jan@example.com", "Jan", UserRole.Member)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nope")]
    [InlineData("jan@@example.com")]
    public void Create_InvalidEmail_IsInvalid(string email)
    {
        var result = _create.Validate(new CreateUserRequest(email, "Jan", UserRole.Member));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Email));
    }

    [Fact]
    public void Create_TooLongFields_AreInvalid()
    {
        var result = _create.Validate(new CreateUserRequest(
            new string('a', User.MaxEmailLength) + "@example.com",
            new string('x', User.MaxDisplayNameLength + 1),
            UserRole.Member));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Email));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.DisplayName));
    }

    [Fact]
    public void Update_EmptyDisplayNameOrUndefinedRole_IsInvalid()
    {
        var result = _update.Validate(new UpdateUserRequest("", (UserRole)7));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUserRequest.DisplayName));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUserRequest.Role));
    }

    [Fact]
    public void Update_ValidRequest_IsValid()
    {
        Assert.True(_update.Validate(new UpdateUserRequest("Jan", UserRole.Admin)).IsValid);
    }
}
