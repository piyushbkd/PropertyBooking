namespace PropertyShowingBooking.Api.Domain;

/// <summary>
/// One line in a booking's history: what it changed from and to, who did it, when, and why.
/// A new entry is written for every successful state change, including the first one (booking created).
/// </summary>
public class BookingHistoryEntry
{
    /// <summary>
    /// The entry's id, given by the database.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// The booking this entry belongs to.
    /// </summary>
    public int BookingId { get; private set; }

    /// <summary>
    /// Empty (null) for the first entry, because the booking did not exist before.
    /// </summary>
    public BookingStatus? FromStatus { get; private set; }

    /// <summary>
    /// The state the booking changed to.
    /// </summary>
    public BookingStatus ToStatus { get; private set; }

    /// <summary>
    /// Who made the change: the owner or the renter.
    /// </summary>
    public Actor Actor { get; private set; }

    /// <summary>
    /// Optional free text given with the change, for example "Plans changed". Null when none was given.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// When the change happened, in UTC.
    /// </summary>
    public DateTime AtUtc { get; private set; }

    // Used by EF Core when it reads an entry from the database.
    private BookingHistoryEntry() { }

    // Only Booking creates entries, so a history line can never be made up from outside.
    internal BookingHistoryEntry(BookingStatus? fromStatus, BookingStatus toStatus, Actor actor, string? reason, DateTime atUtc)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Actor = actor;
        Reason = reason;
        AtUtc = atUtc;
    }
}
