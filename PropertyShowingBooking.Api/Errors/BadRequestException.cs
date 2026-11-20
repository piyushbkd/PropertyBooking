namespace PropertyShowingBooking.Api.Errors;

/// <summary>
/// The request itself is wrong, whatever is in the database, but the problem can only be found
/// in code rather than by a simple attribute. Examples: startsAt is in the past, or "from" is later than "to".
/// Simple checks such as [Range(1, 6)] are handled by ASP.NET Core automatically and also give 400.
/// The API turns this into a 400 Bad Request response.
/// </summary>
public sealed class BadRequestException(string message) : Exception(message);
