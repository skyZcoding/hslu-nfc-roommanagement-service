using RoomManagement.Application.Common;
using RoomManagement.Application.Tests.Fakes;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Users;

namespace RoomManagement.Application.Tests.Users;

public class UserHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(Now);

    private CreateUserHandler CreateHandler() =>
        new(_users, _unitOfWork, _clock, new CreateUserRequestValidator());

    private UpdateUserHandler UpdateHandler() => new(_users, _unitOfWork, new UpdateUserRequestValidator());

    private User SeedUser(string email = "jan@example.com", UserRole role = UserRole.Member, bool active = true)
    {
        var user = User.Create(email, "Jan", role, Now);
        if (!active) user.Deactivate();
        _users.Add(user);
        return user;
    }

    // ---- create ----

    [Fact]
    public async Task Create_ValidRequest_PersistsAndReturnsDto()
    {
        var result = await CreateHandler().HandleAsync(new("Jan@Example.com", " Jan ", UserRole.Admin), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("jan@example.com", result.Value.Email);
        Assert.Equal("Jan", result.Value.DisplayName);
        Assert.Equal(UserRole.Admin, result.Value.Role);
        Assert.True(result.Value.IsActive);
        Assert.Equal(Now, result.Value.CreatedAt);
        Assert.Single(_users.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsFieldErrors_AndPersistsNothing()
    {
        var result = await CreateHandler().HandleAsync(new("not-an-email", "", (UserRole)42), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        var fields = result.Error.ValidationErrors!.Keys;
        Assert.Contains(nameof(CreateUserRequest.Email), fields);
        Assert.Contains(nameof(CreateUserRequest.DisplayName), fields);
        Assert.Contains(nameof(CreateUserRequest.Role), fields);
        Assert.Empty(_users.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_DuplicateEmail_IgnoringCase_ReturnsConflict()
    {
        SeedUser("jan@example.com");

        var result = await CreateHandler().HandleAsync(new(" JAN@example.com", "Other", UserRole.Member), Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("user_email_taken", result.Error.Code);
        Assert.Single(_users.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- get ----

    [Fact]
    public async Task Get_Existing_ReturnsDto()
    {
        var user = SeedUser();

        var result = await new GetUserHandler(_users).HandleAsync(user.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.Id);
    }

    [Fact]
    public async Task Get_Unknown_ReturnsNotFound()
    {
        var result = await new GetUserHandler(_users).HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("user_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Get_ValueOfFailedResult_Throws()
    {
        var result = await new GetUserHandler(_users).HandleAsync(Guid.NewGuid(), Ct);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    // ---- list ----

    [Fact]
    public async Task List_NoFilter_ReturnsAll()
    {
        SeedUser("a@example.com");
        SeedUser("b@example.com", active: false);

        var result = await new ListUsersHandler(_users).HandleAsync(new ListUsersQuery(), Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task List_ActiveOnlyAndRole_FiltersResults()
    {
        SeedUser("member@example.com", UserRole.Member);
        SeedUser("admin@example.com", UserRole.Admin);
        SeedUser("inactive-admin@example.com", UserRole.Admin, active: false);
        var handler = new ListUsersHandler(_users);

        var activeOnly = await handler.HandleAsync(new ListUsersQuery(ActiveOnly: true), Ct);
        var admins = await handler.HandleAsync(new ListUsersQuery(Role: UserRole.Admin), Ct);
        var activeAdmins = await handler.HandleAsync(new ListUsersQuery(true, UserRole.Admin), Ct);

        Assert.Equal(2, activeOnly.Count);
        Assert.Equal(2, admins.Count);
        Assert.Equal("admin@example.com", Assert.Single(activeAdmins).Email);
    }

    // ---- update ----

    [Fact]
    public async Task Update_ValidRequest_ChangesUserAndSaves()
    {
        var user = SeedUser();

        var result = await UpdateHandler().HandleAsync(user.Id, new("Jan W.", UserRole.Admin), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Jan W.", result.Value.DisplayName);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_InvalidRequest_ReturnsValidationError_AndKeepsUser()
    {
        var user = SeedUser();

        var result = await UpdateHandler().HandleAsync(user.Id, new("   ", UserRole.Admin), Ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(nameof(UpdateUserRequest.DisplayName), result.Error.ValidationErrors!.Keys);
        Assert.Equal("Jan", user.DisplayName);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        var result = await UpdateHandler().HandleAsync(Guid.NewGuid(), new("Jan", UserRole.Member), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- deactivate ----

    [Fact]
    public async Task Deactivate_Existing_DeactivatesAndSaves()
    {
        var user = SeedUser();

        var result = await new DeactivateUserHandler(_users, _unitOfWork).HandleAsync(user.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_IsIdempotent()
    {
        var user = SeedUser(active: false);

        var result = await new DeactivateUserHandler(_users, _unitOfWork).HandleAsync(user.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task Deactivate_Unknown_ReturnsNotFound()
    {
        var result = await new DeactivateUserHandler(_users, _unitOfWork).HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
