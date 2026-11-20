using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Api.Domain;

namespace PropertyShowingBooking.Api.Data;

/// <summary>
/// The EF Core database context: it maps the domain classes to the SQLite tables
/// Slots, Bookings and BookingHistory, and defines the indexes and checks the database enforces.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingHistoryEntry> BookingHistory => Set<BookingHistoryEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every DateTime column is stored and read back as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Slot>(slot =>
        {
            slot.ToTable("Slots", table =>
                // A last safety net inside the database itself: booked seats can never be
                // negative or above capacity, whatever the application code does.
                table.HasCheckConstraint(
                    "CK_Slots_BookedSeats_WithinCapacity",
                    "\"BookedSeats\" >= 0 AND \"BookedSeats\" <= \"Capacity\""));

            slot.HasKey(s => s.Id);
            slot.Property(s => s.PropertyId).IsRequired().HasMaxLength(100);
            slot.Ignore(s => s.SeatsRemaining); // calculated, not stored

            // Matches the GET /slots filter (by property) and sort order (by start time).
            slot.HasIndex(s => new { s.PropertyId, s.StartsAtUtc });
        });

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.ToTable("Bookings");
            booking.HasKey(b => b.Id);

            booking.Property(b => b.RenterEmail).IsRequired().HasMaxLength(254);

            // Stored as words ("Confirmed"), not numbers, so the database is easy to read.
            // Also a concurrency token: EF Core adds "AND Status = <the status it read>" to every UPDATE,
            // so if two people change the same booking at the same moment, the second one fails
            // instead of silently overwriting the first (and, for a cancel, giving the seats back twice).
            booking.Property(b => b.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();

            booking.HasOne<Slot>()
                .WithMany()
                .HasForeignKey(b => b.SlotId)
                .OnDelete(DeleteBehavior.Restrict);

            // One active booking per renter per slot, enforced by the database.
            // The filter means only Confirmed bookings count, so after a cancel the
            // same renter can book the same slot again.
            booking.HasIndex(b => new { b.SlotId, b.RenterEmail })
                .IsUnique()
                .HasFilter("\"Status\" = 'Confirmed'")
                .HasDatabaseName("UX_Bookings_OneActiveBookingPerRenterPerSlot");

            // The history list is kept in the private field _history, so only Booking can add to it.
            booking.HasMany(b => b.History)
                .WithOne()
                .HasForeignKey(h => h.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            booking.Navigation(b => b.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<BookingHistoryEntry>(entry =>
        {
            entry.ToTable("BookingHistory");
            entry.HasKey(h => h.Id);
            entry.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20);
            entry.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20);
            entry.Property(h => h.Actor).HasConversion<string>().HasMaxLength(10);
            entry.Property(h => h.Reason).HasMaxLength(500);
        });
    }
}
