using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Users;

namespace RoomManagement.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(User.MaxDisplayNameLength).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // The domain stores emails lowercase, so a plain unique index is case-insensitive in practice.
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(DbConstraints.UserEmailUnique);
    }
}
