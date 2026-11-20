using PropertyShowingBooking.Api.Errors;

namespace PropertyShowingBooking.Api.Domain;

/// <summary>
/// One showing time for one property, created by the owner.
/// Example: flat P-101, Saturday 21 Nov 2026 at 10:00 AM, 30 minutes, at most 5 people.
/// </summary>
public class Slot
{
    // The allowed ranges from the brief. The request classes use these same numbers,
    // so the rules are written in one place only.
    /// <summary>
    /// The shortest slot: 15 minutes.
    /// </summary>
    public const int MinDurationMinutes = 15;

    /// <summary>
    /// The longest slot: 120 minutes.
    /// </summary>
    public const int MaxDurationMinutes = 120;

    /// <summary>
    /// The fewest seats a slot can have: 1.
    /// </summary>
    public const int MinCapacity = 1;

    /// <summary>
    /// The most seats a slot can have: 20.
    /// </summary>
    public const int MaxCapacity = 20;

    /// <summary>
    /// The slot's id, given by the database.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// Which property is being shown, for example "P-101". Stored without spaces around it.
    /// </summary>
    public string PropertyId { get; private set; } = string.Empty;

    /// <summary>
    /// Start time, always stored in UTC.
    /// </summary>
    public DateTime StartsAtUtc { get; private set; }

    /// <summary>
    /// How long the showing lasts, 15 to 120 minutes.
    /// </summary>
    public int DurationMinutes { get; private set; }

    /// <summary>
    /// How many people can attend in total, 1 to 20.
    /// </summary>
    public int Capacity { get; private set; }

    /// <summary>
    /// Seats taken by all bookings that are not Cancelled.
    /// It is changed only by a single database statement that checks and updates it in one step,
    /// so it can never go above Capacity, even when many people book at the same moment.
    /// </summary>
    public int BookedSeats { get; private set; }

    /// <summary>
    /// How many seats are still free.
    /// </summary>
    public int SeatsRemaining => Capacity - BookedSeats;

    // Used by EF Core when it reads a slot from the database.
    private Slot() { }

    /// <summary>
    /// Creates a new slot with no seats booked. This is the only way to create a slot, so every slot
    /// follows the rules: it starts in the future, lasts 15 to 120 minutes and has 1 to 20 seats.
    /// The request attributes check the ranges first and give a clear 400; these checks are the safety net.
    /// </summary>
    /// <exception cref="BadRequestException">A value breaks one of the rules above.</exception>
    public static Slot Create(string propertyId, DateTime startsAtUtc, int durationMinutes, int capacity, DateTime nowUtc)
    {
        if (startsAtUtc <= nowUtc)
        {
            throw new BadRequestException("startsAt must be in the future.");
        }
        if (durationMinutes is < MinDurationMinutes or > MaxDurationMinutes)
        {
            throw new BadRequestException(
                $"durationMinutes must be between {MinDurationMinutes} and {MaxDurationMinutes}.");
        }
        if (capacity is < MinCapacity or > MaxCapacity)
        {
            throw new BadRequestException($"capacity must be between {MinCapacity} and {MaxCapacity}.");
        }

        return new Slot
        {
            PropertyId = propertyId.Trim(),
            StartsAtUtc = startsAtUtc,
            DurationMinutes = durationMinutes,
            Capacity = capacity,
            BookedSeats = 0
        };
    }

    /// <summary>
    /// Whether this slot has started at the given time. A slot counts as started from its exact start
    /// time onwards. Example: a slot at 10:00:00 has started at 10:00:00 and at 10:05.
    /// This is the only place that decides when a slot has started; Booking is told the answer.
    /// </summary>
    public bool HasStarted(DateTime nowUtc) => nowUtc >= StartsAtUtc;

    /// <summary>
    /// Refuses a new booking once the slot has started. Whether enough seats are left is not checked
    /// here: only the database can check that safely when many people book at the same moment.
    /// </summary>
    /// <exception cref="ConflictException">The slot has already started.</exception>
    public void EnsureCanBeBooked(DateTime nowUtc)
    {
        if (HasStarted(nowUtc))
        {
            throw new ConflictException("The slot has already started, so it can no longer be booked.");
        }
    }
}
