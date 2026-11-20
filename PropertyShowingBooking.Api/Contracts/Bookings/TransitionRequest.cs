using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PropertyShowingBooking.Api.Contracts.Bookings;

/// <summary>
/// Body of POST /bookings/{id}/transitions, for example
/// <c>{ "to": "Cancelled", "reason": "Plans changed", "actor": "renter" }</c>.
/// An unknown state name or actor is a 400. A known change that the rules do not allow is a 409.
/// </summary>
public sealed class TransitionRequest
{
    /// <summary>
    /// The new state: "Cancelled", "Attended" or "NoShow".
    /// </summary>
    [Required]
    public BookingStatus? To { get; init; }

    /// <summary>
    /// Optional free text, for example "Plans changed".
    /// </summary>
    [StringLength(500)]
    public string? Reason { get; init; }

    /// <summary>
    /// Who is making the change: "owner" or "renter".
    /// </summary>
    [Required]
    [JsonConverter(typeof(LowerCaseEnumConverter<Actor>))]
    public Actor? Actor { get; init; }
}
