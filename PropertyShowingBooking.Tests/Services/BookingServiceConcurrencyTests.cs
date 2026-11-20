using AutoFixture;
using FluentAssertions;
using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Errors;
using PropertyShowingBooking.Tests.TestSupport;
using System.Net.Mail;

namespace PropertyShowingBooking.Tests.Services;

// "If several renters book the last seats of a slot at the same moment, the slot must not be overbooked."
// Many requests start together, each with its own database context (like separate web requests),
// against a real SQLite file.
public sealed class BookingServiceConcurrencyTests : IDisposable
{
    private readonly Fixture _fixture = new();
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    // [MethodUnderTest]_[Scenario]_[ExpectedBehavior]

    [Fact]
    public async Task BookAsync_TwentyRentersForFiveSeatsAtSameMoment_ShouldConfirmExactlyFive()
    {
        // Arrange
        var slot = await _db.CreateSlotAsync(capacity: 5);
        List<string> renters = [.. Enumerable.Range(0, 20).Select(_ => _fixture.Create<MailAddress>().Address)];

        // Act
        var outcome = await RunAtTheSameMoment(renters.Count, i => _db.BookAsync(slot.Id, renters[i], partySize: 1));

        // Assert
        outcome.Succeeded.Should().Be(5);
        outcome.Refused.Should().Be(15);
        (await _db.ReadSlotAsync(slot.Id)).SeatsRemaining.Should().Be(0);
        await _db.AssertSeatsAreConsistentAsync(slot.Id);
    }

    [Fact]
    public async Task TransitionAsync_CancelSameBookingManyTimesAtSameMoment_ShouldFreeSeatsOnlyOnce()
    {
        // Arrange
        var slot = await _db.CreateSlotAsync(capacity: 10);
        await _db.BookAsync(slot.Id, _fixture.Create<MailAddress>().Address, 2);
        var booking = await _db.BookAsync(slot.Id, _fixture.Create<MailAddress>().Address, 4);

        // Act: owner and renter both press "cancel" again and again at the same moment.
        var outcome = await RunAtTheSameMoment(10, i =>
            _db.TransitionAsync(booking.Id, BookingStatus.Cancelled, i % 2 == 0 ? Actor.Owner : Actor.Renter));

        // Assert
        outcome.Succeeded.Should().Be(1);
        outcome.Refused.Should().Be(9);
        (await _db.ReadSlotAsync(slot.Id)).SeatsRemaining.Should().Be(8); // 10 - the other 2; the 4 returned once
        await _db.AssertSeatsAreConsistentAsync(slot.Id);

        using var context = _db.NewContext();
        var history = await _db.NewBookingService(context).GetHistoryAsync(booking.Id, CancellationToken.None);
        history.Should().HaveCount(2); // Confirmed, then Cancelled once
    }

    private sealed record Outcome(int Succeeded, int Refused);

    /// <summary>
    /// Starts <paramref name="count"/> operations on separate threads, releases them all at once, and counts
    /// how many succeeded and how many were refused with 409 (ConflictException). Any other exception,
    /// for example "database is locked", fails the test, because it would be a 500 for a real user.
    /// </summary>
    private static async Task<Outcome> RunAtTheSameMoment(int count, Func<int, Task> operation)
    {
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        List<Task<bool>> tasks = [.. Enumerable.Range(0, count).Select(i => Task.Run(async () =>
        {
            await startSignal.Task; // wait until every task is ready, then all go together
            try
            {
                await operation(i);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }))];

        startSignal.SetResult();
        var results = await Task.WhenAll(tasks); // an unexpected exception fails the test here

        return new Outcome(results.Count(ok => ok), results.Count(ok => !ok));
    }
}
