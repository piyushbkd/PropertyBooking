using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PropertyShowingBooking.Api.Data;

/// <summary>
/// Makes sure every DateTime is saved as UTC and comes back from the database marked as UTC.
/// SQLite has no real date type: it stores dates as text and forgets whether they were UTC.
/// Without this, a time read back from the database would have Kind = Unspecified, and comparing it
/// with the current UTC time could give wrong answers.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    // Saving: a local time is changed to UTC; any other time is saved as it is.
    toDatabase => toDatabase.Kind == DateTimeKind.Local ? toDatabase.ToUniversalTime() : toDatabase,
    // Reading: SQLite gives the time back with no kind, so mark it as UTC.
    fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));
