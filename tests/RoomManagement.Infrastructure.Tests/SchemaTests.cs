using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Infrastructure.Persistence;

namespace RoomManagement.Infrastructure.Tests;

[Collection(PostgresCollection.Name)]
public class SchemaTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    [Fact]
    public async Task Migrations_MatchTheEfModel()
    {
        var hasPendingChanges = await InScope(sp =>
            Task.FromResult(sp.GetRequiredService<AppDbContext>().Database.HasPendingModelChanges()));

        Assert.False(hasPendingChanges, "The model changed; add a migration (dotnet ef migrations add ...).");
    }

    [Fact]
    public async Task Database_ContainsTheConstraintsAndIndexesThatTheRulesDependOn()
    {
        var (constraints, indexes) = await InScope(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var constraints = await db.Database
                .SqlQuery<string>($"SELECT conname AS \"Value\" FROM pg_constraint WHERE connamespace = 'public'::regnamespace")
                .ToListAsync(Ct);
            var indexes = await db.Database
                .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
                .ToListAsync(Ct);
            return (constraints, indexes);
        });

        Assert.Contains("ex_bookings_room_no_overlap", constraints);
        Assert.Contains("ck_bookings_ends_after_start", constraints);
        Assert.Contains("ck_rooms_capacity_positive", constraints);
        Assert.Contains("fk_bookings_rooms_room_id", constraints);
        Assert.Contains("fk_bookings_users_user_id", constraints);
        Assert.Contains("ix_users_email", indexes);
        Assert.Contains("ix_rooms_name_lower", indexes);
        Assert.Contains("ix_rooms_reader_id", indexes);
    }

    [Fact]
    public async Task Tables_UseSnakeCaseNames()
    {
        var columns = await InScope(sp => sp.GetRequiredService<AppDbContext>().Database
            .SqlQuery<string>($"SELECT table_name || '.' || column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public'")
            .ToListAsync(Ct));

        Assert.Contains("users.display_name", columns);
        Assert.Contains("rooms.reader_id", columns);
        Assert.Contains("bookings.starts_at", columns);
        Assert.Contains("bookings.ends_at", columns);
        Assert.Contains("bookings.checked_in_at", columns);
    }
}
