using Microsoft.EntityFrameworkCore;
using RoomManagement.Domain.Bookings;
using RoomManagement.Domain.Rooms;
using RoomManagement.Domain.Users;

namespace RoomManagement.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The overlap constraint on bookings needs "=" on uuid columns inside a GiST index.
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
