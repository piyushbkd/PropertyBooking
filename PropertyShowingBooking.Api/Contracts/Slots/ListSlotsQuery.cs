using System.ComponentModel.DataAnnotations;

namespace PropertyShowingBooking.Api.Contracts.Slots;

/// <summary>
/// Query string of GET /slots. All filters are optional.
/// Example: /slots?propertyId=P-101&amp;from=2026-12-01T00:00:00Z&amp;to=2026-12-08T00:00:00Z&amp;page=1&amp;pageSize=20
/// </summary>
public sealed class ListSlotsQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    [StringLength(100)]
    public string? PropertyId { get; init; }

    /// <summary>
    /// Only slots starting at or after this time.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Only slots starting before this time.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;
}
