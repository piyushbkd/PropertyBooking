using PropertyShowingBooking.Api.Domain;

namespace PropertyShowingBooking.Api.Contracts.Bookings;

/// <summary>
/// One booking as returned by the API.
/// </summary>
public sealed record BookingResponse(
    int Id,
    int SlotId,
    string RenterEmail,
    int PartySize,
    BookingStatus Status,
    DateTime CreatedAt)
{
    public static BookingResponse From(Booking booking) => new(
        booking.Id,
        booking.SlotId,
        booking.RenterEmail,
        booking.PartySize,
        booking.Status,
        booking.CreatedAtUtc);
}
