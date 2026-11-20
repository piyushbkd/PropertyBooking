namespace PropertyShowingBooking.Api.Domain;

/// <summary>
/// The states a booking can be in.
/// A booking starts as Confirmed. Cancelled, Attended and NoShow are final:
/// once a booking reaches one of them, it can never change again.
/// </summary>
public enum BookingStatus
{
    /// <summary>
    /// The booking holds its seats. Every booking starts in this state.
    /// </summary>
    Confirmed,

    /// <summary>
    /// Cancelled by the renter or the owner before the slot started. The seats were given back. Final.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The renter came to the showing, as marked by the owner after the slot started. Final.
    /// </summary>
    Attended,

    /// <summary>
    /// The renter did not come, as marked by the owner after the slot started. Final.
    /// </summary>
    NoShow
}

/// <summary>
/// Helper methods for BookingStatus.
/// </summary>
public static class BookingStatusExtensions
{
    /// <summary>
    /// True for Cancelled, Attended and NoShow, which can never change again.
    /// </summary>
    public static bool IsFinal(this BookingStatus status) => status != BookingStatus.Confirmed;
}
