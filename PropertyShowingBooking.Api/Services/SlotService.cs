using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Api.Contracts;
using PropertyShowingBooking.Api.Contracts.Slots;
using PropertyShowingBooking.Api.Data;
using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Errors;
using PropertyShowingBooking.Api.Services.Interfaces;

namespace PropertyShowingBooking.Api.Services;

/// <summary>
/// Creating and listing showing slots.
/// </summary>
public sealed class SlotService(AppDbContext db, TimeProvider time) : ISlotService
{
    /// <summary>
    /// Creates a slot. The attribute checks (duration 15 to 120, capacity 1 to 20) have already run.
    /// The slot rules, including "the start time must be in the future", are checked by <see cref="Slot.Create"/>.
    /// </summary>
    public async Task<SlotResponse> CreateAsync(CreateSlotRequest request, CancellationToken cancellationToken)
    {
        var nowUtc = time.GetUtcNow().UtcDateTime;

        var slot = Slot.Create(
            request.PropertyId,
            request.StartsAt!.Value.UtcDateTime,
            request.DurationMinutes, 
            request.Capacity, 
            nowUtc);

        db.Slots.Add(slot);

        await db.SaveChangesAsync(cancellationToken);

        return SlotResponse.From(slot);
    }

    /// <summary>
    /// Gets one slot, including how many seats are still free. Refused with 404 if the slot does not exist.
    /// </summary>
    public async Task<SlotResponse> GetAsync(int slotId, CancellationToken cancellationToken)
    {
        var slot = await db.Slots
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == slotId, cancellationToken) 
                ?? throw new NotFoundException($"Slot {slotId} was not found.");

        return SlotResponse.From(slot);
    }

    /// <summary>
    /// Lists slots, earliest start first, filtered and one page at a time.
    /// "from" includes its time; "to" excludes its time.
    /// </summary>
    public async Task<PagedResponse<SlotResponse>> ListAsync(ListSlotsQuery query, CancellationToken cancellationToken)
    {
        var fromUtc = query.From?.UtcDateTime;
        var toUtc = query.To?.UtcDateTime;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
            throw new BadRequestException("\"from\" must not be later than \"to\".");

        var slots = db.Slots.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.PropertyId))
        {
            var propertyId = query.PropertyId.Trim();
            slots = slots.Where(s => s.PropertyId == propertyId);
        }

        if (fromUtc.HasValue)
            slots = slots.Where(s => s.StartsAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            slots = slots.Where(s => s.StartsAtUtc < toUtc.Value);

        var totalCount = await slots.CountAsync(cancellationToken);

        // (page - 1) * pageSize is worked out as a long, so a huge page number cannot overflow.
        var skip = (int)Math.Min((long)(query.Page - 1) * query.PageSize, int.MaxValue);

        var page = await slots
            .OrderBy(s => s.StartsAtUtc)
            .ThenBy(s => s.Id) // same start time: a stable, predictable order
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return new PagedResponse<SlotResponse>(
            [.. page.Select(SlotResponse.From)],
            query.Page,
            query.PageSize,
            totalCount,
            totalPages);
    }
}
