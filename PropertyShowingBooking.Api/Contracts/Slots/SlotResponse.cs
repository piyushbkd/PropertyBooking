using PropertyShowingBooking.Api.Domain;

namespace PropertyShowingBooking.Api.Contracts.Slots;

/// <summary>
/// One slot as returned by the API, including how many seats are still free.
/// </summary>
public sealed record SlotResponse(
    int Id,
    string PropertyId,
    DateTime StartsAt,
    DateTime EndsAt,
    int DurationMinutes,
    int Capacity,
    int SeatsRemaining)
{
    public static SlotResponse From(Slot slot) => new(
        slot.Id,
        slot.PropertyId,
        slot.StartsAtUtc,
        slot.StartsAtUtc.AddMinutes(slot.DurationMinutes),
        slot.DurationMinutes,
        slot.Capacity,
        slot.SeatsRemaining);
}
