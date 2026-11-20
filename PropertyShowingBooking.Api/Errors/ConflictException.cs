namespace PropertyShowingBooking.Api.Errors;

/// <summary>
/// The request is well formed, but the current data does not allow it.
/// Examples: not enough seats left, or the booking is already in a final state.
/// The API turns this into a 409 Conflict response (added in Step 5).
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
