using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Json;
using System.Text.Json.Serialization;

namespace PropertyShowingBooking.Api.Contracts.Bookings;

/// <summary>
/// One line of GET /bookings/{id}/history.
/// </summary>
public sealed record BookingHistoryEntryResponse(
    BookingStatus? FromStatus,
    BookingStatus ToStatus,
    [property: JsonConverter(typeof(LowerCaseEnumConverter<Actor>))] Actor Actor,
    string? Reason,
    DateTime At)
{
    public static BookingHistoryEntryResponse From(BookingHistoryEntry entry) => new(
        entry.FromStatus,
        entry.ToStatus,
        entry.Actor,
        entry.Reason,
        entry.AtUtc);
}
