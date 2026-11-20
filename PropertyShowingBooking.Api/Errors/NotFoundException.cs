namespace PropertyShowingBooking.Api.Errors;

/// <summary>
/// The slot or booking named in the URL does not exist.
/// Example: POST /slots/99/bookings when there is no slot 99.
/// The API turns this into a 404 Not Found response.
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);
