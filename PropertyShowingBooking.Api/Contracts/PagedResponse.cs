namespace PropertyShowingBooking.Api.Contracts;

/// <summary>
/// One page of results, plus what the caller needs to fetch the other pages.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
