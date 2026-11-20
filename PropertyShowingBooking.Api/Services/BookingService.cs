using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Api.Contracts.Bookings;
using PropertyShowingBooking.Api.Data;
using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Errors;
using PropertyShowingBooking.Api.Services.Interfaces;

namespace PropertyShowingBooking.Api.Services;

/// <summary>
/// Booking a slot, changing a booking's state, and reading a booking's history.
///
/// How double booking and overbooking are prevented, even when requests arrive at the same moment:
/// <list type="number">
/// <item>Each change runs in a database transaction. SQLite lets only one write transaction run at a
/// time; the others wait (Microsoft.Data.Sqlite starts transactions as "BEGIN IMMEDIATE", which takes
/// the write lock at the start, so the checks inside a transaction always see the latest data).</item>
/// <item>Seats are taken with one UPDATE that checks and changes BookedSeats in the same statement,
/// so it can never go above capacity. This also works on databases that lock single rows, such as SQL Server.</item>
/// <item>Safety nets in the database itself: a unique index allows only one Confirmed booking per renter
/// per slot, and a check constraint keeps BookedSeats between 0 and Capacity.</item>
/// </list>
/// </summary>
public sealed class BookingService(AppDbContext db, TimeProvider time) : IBookingService
{
    // SQLite's error code for "a UNIQUE index was broken".
    private const int SqliteUniqueConstraintFailed = 2067;

    /// <summary>
    /// Books seats in a slot for a renter's party. The booking starts as Confirmed.
    /// Refused with 409 if the slot has started, the renter already has an active booking for it,
    /// or there are not enough seats left; 404 if the slot does not exist.
    /// </summary>
    public async Task<BookingResponse> BookAsync(int slotId, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var nowUtc = time.GetUtcNow().UtcDateTime;
        var renterEmail = Booking.NormalizeEmail(request.RenterEmail);

        // Why our own transaction, when SaveChanges already uses one: SaveChanges covers only its own
        // INSERTs. The seat UPDATE below (ExecuteUpdateAsync) runs and is saved straight away, outside it.
        // Our transaction makes the checks, the seat UPDATE and the INSERTs one all-or-nothing step:
        // if SaveChanges fails, the seats taken are given back. It also starts as "BEGIN IMMEDIATE",
        // so a second request waits here and its checks see this booking.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var slot = await db.Slots
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == slotId, cancellationToken)
                ?? throw new NotFoundException($"Slot {slotId} was not found.");

        // If the slot is started, renter can no longer book
        slot.EnsureCanBeBooked(nowUtc);

        // New booking from the same renter is not allowed for the same slot
        var alreadyHasActiveBooking = await db.Bookings.AnyAsync(
            b => b.SlotId == slotId && b.RenterEmail == renterEmail && b.Status == BookingStatus.Confirmed, cancellationToken);

        if (alreadyHasActiveBooking)
        {
            throw new ConflictException($"{renterEmail} already has an active booking for slot {slotId}.");
        }

        // Check the seats and take them in one statement:
        // UPDATE Slots SET BookedSeats = BookedSeats + @partySize
        // WHERE Id = @slotId AND BookedSeats + @partySize <= Capacity
        var partySize = request.PartySize;
        var rowsChanged = await db.Slots
            .Where(s => s.Id == slotId && s.BookedSeats + partySize <= s.Capacity)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.BookedSeats, s => s.BookedSeats + partySize), cancellationToken);

        if (rowsChanged == 0)
        {
            throw new ConflictException(
                $"Not enough seats left in slot {slotId}: {slot.SeatsRemaining} left, {partySize} requested.");
        }

        var booking = Booking.Create(slotId, renterEmail, partySize, nowUtc);
        db.Bookings.Add(booking); // also saves the first history entry

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraintFailed })
        {
            // Safety net: the database has a rule that a renter can hold only one Confirmed booking per slot.
            // The rule is a unique filtered index on Bookings (SlotId, RenterEmail) WHERE Status = 'Confirmed',
            // so cancelled or finished bookings don't count and the renter can book the same slot again.
            // If a second Confirmed booking slips past the AnyAsync check above (possible on SQL Server, or from
            // future code that skips the check), the index refuses the INSERT and SQLite reports error 2067.
            // This catch makes sure the caller then gets a clear 409 Conflict and not a confusing 500 Internal Server Error.
            // It also makes sure the seats taken a moment earlier are given back
            throw new ConflictException($"{renterEmail} already has an active booking for slot {slotId}.");
        }

        await transaction.CommitAsync(cancellationToken);
        return BookingResponse.From(booking);
    }

    /// <summary>
    /// Changes a booking's state (Cancelled, Attended or NoShow) if the rules allow it, and records the
    /// change in its history. Cancelling gives the seats back. Refused with 409 if the rules do not allow
    /// the change or someone else changed the booking at the same moment; 404 if the booking does not exist.
    /// </summary>
    public async Task<BookingResponse> TransitionAsync(int bookingId, TransitionRequest request, CancellationToken cancellationToken)
    {
        var nowUtc = time.GetUtcNow().UtcDateTime;

        // Why our own transaction: SaveChanges (new status and history entry) and the seat UPDATE
        // (ExecuteUpdateAsync, which runs outside SaveChanges) must succeed or fail together.
        // Without it, a booking could be saved as Cancelled while its seats are never given back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new NotFoundException($"Booking {bookingId} was not found."); // EF remembers: like original Status = "Confirmed"

        var slot = await db.Slots.AsNoTracking().SingleAsync(s => s.Id == booking.SlotId, cancellationToken);

        // The slot answers "have you started?"; the booking only receives that fact. The booking then
        // decides whether the change is allowed (throws ConflictException if not) and adds the new history entry.
        booking.TransitionTo(
            request.To!.Value,
            request.Actor!.Value,
            request.Reason,
            nowUtc,
            slotHasStarted: slot.HasStarted(nowUtc));

        try
        {
            // Status is a concurrency token, so EF Core writes:
            // UPDATE Bookings SET Status = @new WHERE Id = @id AND Status = 'Confirmed'
            // If someone else changed the booking first, 0 rows change and EF Core throws.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // If exception is not catch here and retur explicitly 408,
            // GlobalExceptionHandler would return a 500 (“server bug”) instead of a 409 (“someone else changed it, try again”).
            throw new ConflictException($"Booking {bookingId} was changed by someone else at the same time. Please try again.");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            /*
             * Status concurrency token doesn’t help here. It protects one booking row, and here we are changing the slot Capacity.
             * so neither one has a conflict. The collision is on the slot row.
             * 
             * Through the ExecuteUpdateAsync, database reads and changes the value in one step, on the latest value. 
             * So no update can be lost.
             * 
             * Benefits:
             * We don’t need to load the slot for changing. It’s already loaded with AsNoTracking(), only to ask HasStarted.
             * BookedSeats has a private setter, so the service can’t write slot.BookedSeats -= 3 anyway.
             * 
             * Tradeoff:
             * ExecuteUpdateAsync runs outside SaveChanges. That’s the reason for the explicit transaction, 
             * so that the status change and the seat refund succeed or fail together.
             */

            // Give the seats back.
            var partySize = booking.PartySize;
            await db.Slots
                .Where(s => s.Id == booking.SlotId)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.BookedSeats, s => s.BookedSeats - partySize), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return BookingResponse.From(booking);
    }

    /// <summary>
    /// Gets one booking. Refused with 404 if the booking does not exist.
    /// </summary>
    public async Task<BookingResponse> GetAsync(int bookingId, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new NotFoundException($"Booking {bookingId} was not found.");

        return BookingResponse.From(booking);
    }

    /// <summary>
    /// Every state change of a booking, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<BookingHistoryEntryResponse>> GetHistoryAsync(int bookingId, CancellationToken cancellationToken)
    {
        var exists = await db.Bookings.AnyAsync(b => b.Id == bookingId, cancellationToken);
        if (!exists)
            throw new NotFoundException($"Booking {bookingId} was not found.");

        var entries = await db.BookingHistory
            .AsNoTracking()
            .Where(h => h.BookingId == bookingId)
            .OrderBy(h => h.AtUtc)
            .ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);

        return [.. entries.Select(BookingHistoryEntryResponse.From)];
    }
}
