using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using PropertyShowingBooking.Api.Contracts.Bookings;
using PropertyShowingBooking.Api.Contracts.Slots;
using PropertyShowingBooking.Api.Data;
using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Services;

namespace PropertyShowingBooking.Tests.TestSupport;

/// <summary>
/// A real SQLite database in a new temporary file, created with the real migrations, plus a fake clock.
/// Each test gets its own, so tests never share data. The file is deleted at the end.
///
/// A file is used instead of an in-memory SQLite database on purpose: an in-memory database lives on one
/// shared connection, so requests could never really run at the same moment and the concurrency tests
/// would prove nothing.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    /// <summary>
    /// "Now" in every test: Thursday 19 Nov 2026, 12:00 UTC.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"showings-test-{Guid.NewGuid():N}.db");
    private readonly DbContextOptions<AppDbContext> _options;

    public FakeTimeProvider Clock { get; } = new(Now);

    public TestDatabase()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_filePath}")
            .Options;

        using var db = NewContext();
        db.Database.Migrate();
    }

    /// <summary>
    /// A new, separate database context, like the one each web request gets.
    /// </summary>
    public AppDbContext NewContext() => new(_options);

    public BookingService NewBookingService(AppDbContext db) => new(db, Clock);
    public SlotService NewSlotService(AppDbContext db) => new(db, Clock);

    /// <summary>
    /// Creates a slot that starts one day from <see cref="Now"/>.
    /// </summary>
    public async Task<SlotResponse> CreateSlotAsync(int capacity = 5, string propertyId = "P-101", TimeSpan? startsIn = null)
    {
        using var db = NewContext();
        return await NewSlotService(db).CreateAsync(new CreateSlotRequest
        {
            PropertyId = propertyId,
            StartsAt = Now + (startsIn ?? TimeSpan.FromDays(1)),
            DurationMinutes = 30,
            Capacity = capacity
        }, CancellationToken.None);
    }

    public async Task<BookingResponse> BookAsync(int slotId, string renterEmail, int partySize)
    {
        using var db = NewContext();
        return await NewBookingService(db).BookAsync(
            slotId, new CreateBookingRequest { RenterEmail = renterEmail, PartySize = partySize }, CancellationToken.None);
    }

    public async Task<BookingResponse> TransitionAsync(int bookingId, BookingStatus to, Actor actor, string? reason = null)
    {
        using var db = NewContext();
        return await NewBookingService(db).TransitionAsync(
            bookingId, new TransitionRequest { To = to, Actor = actor, Reason = reason }, CancellationToken.None);
    }

    public async Task<Slot> ReadSlotAsync(int slotId)
    {
        using var db = NewContext();
        return await db.Slots.AsNoTracking().SingleAsync(s => s.Id == slotId);
    }

    /// <summary>
    /// Checks the rule that must always hold: the slot's BookedSeats equals the party sizes of all its
    /// bookings that are not Cancelled, and is never above capacity.
    /// </summary>
    public async Task AssertSeatsAreConsistentAsync(int slotId)
    {
        using var db = NewContext();
        var slot = await db.Slots.AsNoTracking().SingleAsync(s => s.Id == slotId);
        var seatsHeldByBookings = await db.Bookings
            .Where(b => b.SlotId == slotId && b.Status != BookingStatus.Cancelled)
            .SumAsync(b => b.PartySize);

        slot.BookedSeats.Should().Be(seatsHeldByBookings);
        slot.BookedSeats.Should().BeInRange(0, slot.Capacity);
    }

    public void Dispose()
    {
        // Close pooled connections so Windows lets us delete the file.
        SqliteConnection.ClearAllPools();
        string[] paths = [_filePath, _filePath + "-wal", _filePath + "-shm"];
        foreach (var path in paths)
        {
            try { File.Delete(path); } catch (IOException) { /* best effort: it is a temp file */ }
        }
    }
}
