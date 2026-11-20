namespace PropertyShowingBooking.Api.Domain;

/// <summary>
/// Who is making a change. There is no login in this API, so the caller states it in the request.
/// </summary>
public enum Actor
{
    /// <summary>
    /// The property owner (or their agent). May cancel before the slot starts, and mark Attended or NoShow after it starts.
    /// </summary>
    Owner,

    /// <summary>
    /// The person who booked. May only cancel, and only before the slot starts.
    /// </summary>
    Renter
}
