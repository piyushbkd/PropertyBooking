using PropertyShowingBooking.Api.Domain;
using System.ComponentModel.DataAnnotations;

namespace PropertyShowingBooking.Api.Contracts.Slots;

/// <summary>
/// Body of POST /slots. The attributes are checked by ASP.NET Core before the controller runs;
/// any failure returns 400 automatically. The ranges come from <see cref="Slot"/>, so they are written once.
/// </summary>
public sealed class CreateSlotRequest
{
    /// <summary>
    /// Which property is being shown, for example "P-101".
    /// </summary>
    [Required]
    [StringLength(100)]
    public string PropertyId { get; init; } = string.Empty;

    /// <summary>
    /// When the showing starts. Must be in the future. Send it with a time zone, for example
    /// "2026-12-05T10:00:00+05:30" or "2026-12-05T04:30:00Z". It is stored in UTC.
    /// </summary>
    [Required]
    public DateTimeOffset? StartsAt { get; init; }

    [Range(Slot.MinDurationMinutes, Slot.MaxDurationMinutes)]
    public int DurationMinutes { get; init; }

    [Range(Slot.MinCapacity, Slot.MaxCapacity)]
    public int Capacity { get; init; }
}
