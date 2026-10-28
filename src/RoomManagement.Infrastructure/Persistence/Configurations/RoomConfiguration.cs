using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms", t => t.HasCheckConstraint("ck_rooms_capacity_positive", "capacity > 0"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(Room.MaxNameLength).IsRequired();
        builder.Property(r => r.Building).HasMaxLength(Room.MaxBuildingLength).IsRequired();

        builder.Property(r => r.ReaderId)
            .HasConversion(readerId => readerId!.Value, value => ReaderId.Create(value))
            .HasMaxLength(ReaderId.MaxLength);

        // PostgreSQL treats NULLs as distinct in unique indexes, so any number of rooms may have no reader.
        builder.HasIndex(r => r.ReaderId).IsUnique().HasDatabaseName(DbConstraints.RoomReaderUnique);

        // Room names are unique ignoring case; that index is on lower(name), which EF cannot model.
        // It is created with raw SQL in the InitialCreate migration (see DbConstraints.RoomNameUnique).
    }
}
