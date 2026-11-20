using System.Text.Json;
using System.Text.Json.Serialization;

namespace PropertyShowingBooking.Api.Json;

/// <summary>
/// Writes an enum in lower case ("owner", "renter"), as the brief shows for the actor.
/// Reading ignores case, so "Owner" is accepted too. Numbers such as 0 or 1 are refused.
/// Used on the actor fields of the booking contracts; booking states keep their normal
/// spelling ("Cancelled") through the global setting in Program.cs.
/// </summary>
public sealed class LowerCaseEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    where TEnum : struct, Enum;
