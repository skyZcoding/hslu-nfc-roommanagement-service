using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Rooms;
using RoomManagement.Domain.Users;

namespace RoomManagement.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings", t => t.HasCheckConstraint("ck_bookings_ends_after_start", "ends_at > starts_at"));

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        // "end" is a reserved word in SQL, so the columns are not simply called start and end.
        builder.Property(b => b.Start).HasColumnName("starts_at");
        builder.Property(b => b.End).HasColumnName("ends_at");
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Room>().WithMany().HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.RoomId, b.Start });
        builder.HasIndex(b => new { b.UserId, b.Start });

        // The rule "no overlapping non-cancelled bookings per room" is enforced by an exclusion constraint
        // (DbConstraints.BookingNoOverlap) that EF cannot model; the InitialCreate migration creates it with raw SQL.
    }
}
