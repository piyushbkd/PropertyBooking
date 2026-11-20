using Microsoft.AspNetCore.Mvc;
using PropertyShowingBooking.Api.Contracts.Bookings;
using PropertyShowingBooking.Api.Services.Interfaces;

namespace PropertyShowingBooking.Api.Controllers;

/// <summary>
/// The booking endpoints.
/// </summary>
[ApiController]
public sealed class BookingsController(IBookingService bookings) : ControllerBase
{
    /// <summary>
    /// Books seats in a slot for a renter's party. The booking starts as Confirmed.
    /// </summary>
    [HttpPost("slots/{slotId:int}/bookings")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> Book(int slotId, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await bookings.BookAsync(slotId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    /// <summary>
    /// Changes a booking's state: Cancelled, Attended or NoShow.
    /// </summary>
    [HttpPost("bookings/{id:int}/transitions")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BookingResponse> Transition(int id, TransitionRequest request, CancellationToken cancellationToken)
        => bookings.TransitionAsync(id, request, cancellationToken);

    /// <summary>
    /// Every state change of a booking with its time, actor and reason, oldest first.
    /// </summary>
    [HttpGet("bookings/{id:int}/history")]
    [ProducesResponseType<IReadOnlyList<BookingHistoryEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<BookingHistoryEntryResponse>> History(int id, CancellationToken cancellationToken)
        => bookings.GetHistoryAsync(id, cancellationToken);

    /// <summary>
    /// Gets one booking. Not required by the brief, but it gives a new booking a real address
    /// for its Location header.
    /// </summary>
    [HttpGet("bookings/{id:int}")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<BookingResponse> GetById(int id, CancellationToken cancellationToken)
        => bookings.GetAsync(id, cancellationToken);
}
