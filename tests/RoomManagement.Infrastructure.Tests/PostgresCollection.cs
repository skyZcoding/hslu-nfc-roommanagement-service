using RoomManagement.TestSupport;

namespace RoomManagement.Infrastructure.Tests;

/// <summary>Makes all database test classes share one <see cref="PostgresFixture"/> (one container).</summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
