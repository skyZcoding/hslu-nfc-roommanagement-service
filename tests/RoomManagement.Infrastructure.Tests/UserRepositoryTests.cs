using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Application.Common;
using RoomManagement.Application.Users;
using RoomManagement.Domain.Users;

namespace RoomManagement.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public class UserRepositoryTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private Task<User?> GetAsync(Guid id) => InScope(sp => sp.GetRequiredService<IUserRepository>().GetByIdAsync(id, Ct));

    private Task AddAsync(User user) => InScope(async sp =>
    {
        sp.GetRequiredService<IUserRepository>().Add(user);
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    });

    [Fact]
    public async Task Add_ThenGetById_RoundTripsAllFields()
    {
        var user = NewUser("jan@example.com", "Jan Walker", UserRole.Admin);

        await AddAsync(user);
        var loaded = await GetAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.Equal(user.Id, loaded.Id);
        Assert.Equal("jan@example.com", loaded.Email);
        Assert.Equal("Jan Walker", loaded.DisplayName);
        Assert.Equal(UserRole.Admin, loaded.Role);
        Assert.True(loaded.IsActive);
        Assert.Equal(Start, loaded.CreatedAt);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(await GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ChangesToALoadedUser_ArePersistedBySaveChanges()
    {
        var user = NewUser();
        await SeedAsync(user);

        await InScope(async sp =>
        {
            var loaded = await sp.GetRequiredService<IUserRepository>().GetByIdAsync(user.Id, Ct);
            loaded!.Update("Jan W.", UserRole.Admin);
            loaded.Deactivate();
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var reloaded = await GetAsync(user.Id);
        Assert.Equal("Jan W.", reloaded!.DisplayName);
        Assert.Equal(UserRole.Admin, reloaded.Role);
        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public async Task EmailExists_ChecksTheNormalizedEmail()
    {
        await SeedAsync(NewUser("jan@example.com"));

        var exists = await InScope(sp => sp.GetRequiredService<IUserRepository>().EmailExistsAsync("jan@example.com", Ct));
        var missing = await InScope(sp => sp.GetRequiredService<IUserRepository>().EmailExistsAsync("other@example.com", Ct));

        Assert.True(exists);
        Assert.False(missing);
    }

    [Fact]
    public async Task List_FiltersByActiveAndRole_OrderedByDisplayName()
    {
        var carl = NewUser("carl@example.com", "Carl", UserRole.Admin);
        var anna = NewUser("anna@example.com", "Anna", UserRole.Member);
        var bob = NewUser("bob@example.com", "Bob", UserRole.Admin);
        bob.Deactivate();
        await SeedAsync(carl, anna, bob);

        var all = await InScope(sp => sp.GetRequiredService<IUserRepository>().ListAsync(false, null, Ct));
        var active = await InScope(sp => sp.GetRequiredService<IUserRepository>().ListAsync(true, null, Ct));
        var admins = await InScope(sp => sp.GetRequiredService<IUserRepository>().ListAsync(false, UserRole.Admin, Ct));
        var activeAdmins = await InScope(sp => sp.GetRequiredService<IUserRepository>().ListAsync(true, UserRole.Admin, Ct));

        Assert.Equal(["Anna", "Bob", "Carl"], all.Select(u => u.DisplayName));
        Assert.Equal(["Anna", "Carl"], active.Select(u => u.DisplayName));
        Assert.Equal(["Bob", "Carl"], admins.Select(u => u.DisplayName));
        Assert.Equal(["Carl"], activeAdmins.Select(u => u.DisplayName));
    }

    [Fact]
    public async Task Add_DuplicateEmail_ThrowsDataConflict()
    {
        await SeedAsync(NewUser("jan@example.com"));

        var exception = await Assert.ThrowsAsync<DataConflictException>(
            () => AddAsync(NewUser("jan@example.com", "Someone else")));

        Assert.Equal("user_email_taken", exception.Code);
    }
}
