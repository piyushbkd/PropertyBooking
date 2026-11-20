using PropertyShowingBooking.Api.Domain;
using System.ComponentModel.DataAnnotations;

namespace PropertyShowingBooking.Api.Contracts.Bookings;

/// <summary>
/// Body of POST /slots/{id}/bookings.
/// </summary>
public sealed class CreateBookingRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string RenterEmail { get; init; } = string.Empty;

    /// <summary>
    /// How many people are coming together, 1 to 6.
    /// </summary>
    [Range(Booking.MinPartySize, Booking.MaxPartySize)]
    public int PartySize { get; init; }
}
