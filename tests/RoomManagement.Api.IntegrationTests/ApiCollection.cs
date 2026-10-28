namespace RoomManagement.Api.IntegrationTests;

/// <summary>Makes all API test classes share one <see cref="PostgresFixture"/> (one container).</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "api";
}
