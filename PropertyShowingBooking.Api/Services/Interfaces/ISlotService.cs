using PropertyShowingBooking.Api.Contracts;
using PropertyShowingBooking.Api.Contracts.Slots;
using PropertyShowingBooking.Api.Errors;

namespace PropertyShowingBooking.Api.Services.Interfaces;

/// <summary>
/// Creating and listing showing slots. The controllers depend on this interface rather than on
/// <see cref="SlotService"/>, so a controller can be unit-tested with a mocked service (Moq),
/// and another implementation could be swapped in without changing the controllers.
/// </summary>
public interface ISlotService
{
    /// <summary>
    /// Creates a slot.
    /// </summary>
    /// <exception cref="BadRequestException">The start time is not in the future.</exception>
    Task<SlotResponse> CreateAsync(CreateSlotRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one slot.
    /// </summary>
    /// <exception cref="NotFoundException">There is no slot with this id.</exception>
    Task<SlotResponse> GetAsync(int slotId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists slots, earliest start first, filtered and one page at a time.
    /// </summary>
    /// <exception cref="BadRequestException">"from" is later than "to".</exception>
    Task<PagedResponse<SlotResponse>> ListAsync(ListSlotsQuery query, CancellationToken cancellationToken);
}
