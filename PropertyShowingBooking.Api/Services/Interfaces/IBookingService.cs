using PropertyShowingBooking.Api.Contracts.Bookings;
using PropertyShowingBooking.Api.Errors;

namespace PropertyShowingBooking.Api.Services.Interfaces;

/// <summary>
/// Booking a slot, changing a booking's state, and reading a booking's history. The controllers depend on
/// this interface rather than on <see cref="BookingService"/>, so a controller can be unit-tested with a
/// mocked service (Moq), and another implementation could be swapped in without changing the controllers.
/// </summary>
public interface IBookingService
{
    /// <summary>
    /// Books seats in a slot for a renter's party. The booking starts as Confirmed.
    /// </summary>
    /// <exception cref="NotFoundException">There is no slot with this id.</exception>
    /// <exception cref="ConflictException">
    /// The slot has started, the renter already has an active booking for it, or there are not enough seats.
    /// </exception>
    Task<BookingResponse> BookAsync(int slotId, CreateBookingRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Changes a booking's state (Cancelled, Attended or NoShow) if the rules allow it.
    /// </summary>
    /// <exception cref="NotFoundException">There is no booking with this id.</exception>
    /// <exception cref="ConflictException">The rules do not allow this change.</exception>
    Task<BookingResponse> TransitionAsync(int bookingId, TransitionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one booking.
    /// </summary>
    /// <exception cref="NotFoundException">There is no booking with this id.</exception>
    Task<BookingResponse> GetAsync(int bookingId, CancellationToken cancellationToken);

    /// <summary>
    /// Every state change of a booking, oldest first.
    /// </summary>
    /// <exception cref="NotFoundException">There is no booking with this id.</exception>
    Task<IReadOnlyList<BookingHistoryEntryResponse>> GetHistoryAsync(int bookingId, CancellationToken cancellationToken);
}
