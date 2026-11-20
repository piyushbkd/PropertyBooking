using Microsoft.AspNetCore.Mvc;
using PropertyShowingBooking.Api.Contracts;
using PropertyShowingBooking.Api.Contracts.Slots;
using PropertyShowingBooking.Api.Services.Interfaces;

namespace PropertyShowingBooking.Api.Controllers;

/// <summary>
/// The slot endpoints.
/// </summary>
[ApiController]
[Route("slots")]
public sealed class SlotsController(ISlotService slots) : ControllerBase
{
    /// <summary>
    /// Creates a showing slot for a property.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<SlotResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SlotResponse>> Create(CreateSlotRequest request, CancellationToken cancellationToken)
    {
        var slot = await slots.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = slot.Id }, slot);
    }

    /// <summary>
    /// Lists slots ordered by start time, with seats remaining for each.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<SlotResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<SlotResponse>> List([FromQuery] ListSlotsQuery query, CancellationToken cancellationToken)
        => slots.ListAsync(query, cancellationToken);

    /// <summary>
    /// Gets one slot. Not required by the brief, but it gives POST /slots a real address for its
    /// Location header, and lets a caller check seats remaining for a single slot.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<SlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<SlotResponse> GetById(int id, CancellationToken cancellationToken)
        => slots.GetAsync(id, cancellationToken);
}
