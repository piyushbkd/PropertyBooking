using PropertyShowingBooking.Api.Errors;

namespace PropertyShowingBooking.Api.Domain;

/// <summary>
/// One renter's reservation of seats in one slot, for their party (the group coming together).
/// This class holds the state-change rules. It has no database or web code, so the rules can be
/// tested on their own.
/// </summary>
public class Booking
{
    /// <summary>
    /// The smallest party that can book: 1 person.
    /// </summary>
    public const int MinPartySize = 1;

    /// <summary>
    /// The largest party that can book: 6 people.
    /// </summary>
    public const int MaxPartySize = 6;

    private readonly List<BookingHistoryEntry> _history = [];

    /// <summary>
    /// The booking's id, given by the database.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// The slot this booking is for. The only link between a booking and its slot.
    /// </summary>
    public int SlotId { get; private set; }

    /// <summary>
    /// Stored in lowercase, so "Asha@Mail.com" and "asha@mail.com" are the same renter.
    /// </summary>
    public string RenterEmail { get; private set; } = string.Empty;

    /// <summary>
    /// How many people are coming together. This many seats are taken in the slot.
    /// </summary>
    public int PartySize { get; private set; }

    /// <summary>
    /// The booking's current state. Changed only by TransitionTo.
    /// </summary>
    public BookingStatus Status { get; private set; }

    /// <summary>
    /// When the booking was made, in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Every state change of this booking, oldest first.
    /// </summary>
    public IReadOnlyList<BookingHistoryEntry> History => _history;

    // Used by EF Core when it reads a booking from the database.
    private Booking() { }

    /// <summary>
    /// Creates a new booking. It always starts as Confirmed, and its first history entry is
    /// "Confirmed, by renter". Checking that the seats are free happens in the database (Step 7),
    /// because only the database can do that safely when many people book at the same moment.
    /// </summary>
    public static Booking Create(int slotId, string renterEmail, int partySize, DateTime nowUtc)
    {
        var booking = new Booking
        {
            SlotId = slotId,
            RenterEmail = NormalizeEmail(renterEmail),
            PartySize = partySize,
            Status = BookingStatus.Confirmed,
            CreatedAtUtc = nowUtc
        };

        booking._history.Add(new BookingHistoryEntry(
            fromStatus: null,
            toStatus: BookingStatus.Confirmed,
            actor: Actor.Renter,
            reason: null,
            atUtc: nowUtc));

        return booking;
    }

    /// <summary>
    /// Removes spaces around the email and makes it lowercase, so the same renter is always recognised.
    /// </summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Changes the booking to a new state if the rules allow it, and records the change in the history.
    /// The rules:
    /// <list type="bullet">
    /// <item>Cancelled, Attended and NoShow are final. Nothing can change them.</item>
    /// <item>Confirmed to Cancelled: by the renter or the owner, only before the slot starts.</item>
    /// <item>Confirmed to Attended or NoShow: by the owner only, only after the slot starts.</item>
    /// <item>Any other change is refused.</item>
    /// </list>
    /// Booking does not decide when a slot has started: the caller asks the slot
    /// (<see cref="Slot.HasStarted"/>) and passes the answer in <paramref name="slotHasStarted"/>.
    /// This keeps Booking independent of the Slot aggregate; the two are linked only by <see cref="SlotId"/>.
    /// </summary>
    /// <param name="newStatus">The state to change to.</param>
    /// <param name="actor">Who is making the change.</param>
    /// <param name="reason">Optional free text; blank is stored as no reason.</param>
    /// <param name="nowUtc">The current time, recorded in the history entry.</param>
    /// <param name="slotHasStarted">Whether this booking's slot has started at <paramref name="nowUtc"/>.</param>
    /// <returns>The new history entry.</returns>
    /// <exception cref="ConflictException">The rules do not allow this change. The booking is left unchanged.</exception>
    public BookingHistoryEntry TransitionTo(
        BookingStatus newStatus,
        Actor actor,
        string? reason,
        DateTime nowUtc,
        bool slotHasStarted)
    {
        EnsureTransitionIsAllowed(newStatus, actor, slotHasStarted);

        var entry = new BookingHistoryEntry(
            fromStatus: Status,
            toStatus: newStatus,
            actor: actor,
            reason: string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            atUtc: nowUtc);

        Status = newStatus;
        _history.Add(entry);
        return entry;
    }

    /// <summary>
    /// Checks the state rules listed on TransitionTo and throws ConflictException if the change is not allowed.
    /// </summary>
    private void EnsureTransitionIsAllowed(BookingStatus newStatus, Actor actor, bool slotHasStarted)
    {
        if (Status.IsFinal())
        {
            throw new ConflictException(
                $"The booking is already {Status}, which is a final state. It cannot be changed any more.");
        }

        switch (newStatus)
        {
            case BookingStatus.Cancelled:
                // The renter or the owner may cancel, but only before the slot starts.
                if (slotHasStarted)
                {
                    throw new ConflictException(
                        "The slot has already started, so the booking can no longer be cancelled.");
                }
                break;

            case BookingStatus.Attended:
            case BookingStatus.NoShow:
                // Only the owner may record whether the renter came, and only once the slot has started.
                if (actor != Actor.Owner)
                {
                    throw new ConflictException(
                        $"Only the owner can mark a booking as {newStatus}.");
                }
                if (!slotHasStarted)
                {
                    throw new ConflictException(
                        $"The slot has not started yet, so the booking cannot be marked as {newStatus}.");
                }
                break;

            default:
                // The only other state is Confirmed. A booking is never moved back to Confirmed.
                throw new ConflictException(
                    $"A booking cannot be changed to {newStatus}. From Confirmed it can only change to Cancelled, Attended or NoShow.");
        }
    }
}
