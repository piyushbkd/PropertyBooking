using AutoFixture;
using FluentAssertions;
using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Errors;

namespace PropertyShowingBooking.Tests.Domain;

public class BookingTests
{
    private readonly Fixture _fixture = new();

    // The slot starts on Saturday 21 Nov 2026 at 10:00 AM India time (04:30 UTC).
    private static readonly DateTime SlotStartsAtUtc = new(2026, 11, 21, 4, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime OneHourBeforeStart = SlotStartsAtUtc.AddHours(-1);
    private static readonly DateTime FiveMinutesAfterStart = SlotStartsAtUtc.AddMinutes(5);

    // [MethodUnderTest]_[Scenario]_[ExpectedBehavior]

    [Theory]
    [InlineData(Actor.Renter)]
    [InlineData(Actor.Owner)]
    public void TransitionTo_CancelBeforeSlotStarts_ShouldSetStatusToCancelled(Actor actor)
    {
        // Arrange
        var booking = NewConfirmedBooking();

        // Act
        booking.TransitionTo(BookingStatus.Cancelled, actor, _fixture.Create<string>(), OneHourBeforeStart, slotHasStarted: false);

        // Assert
        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Theory]
    [InlineData(Actor.Renter)]
    [InlineData(Actor.Owner)]
    public void TransitionTo_CancelAfterSlotStarts_ShouldThrowConflictExceptionAndKeepStatus(Actor actor)
    {
        // Arrange
        var booking = NewConfirmedBooking();

        // Act
        var act = () => booking.TransitionTo(BookingStatus.Cancelled, actor, null, FiveMinutesAfterStart, slotHasStarted: true);

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("The slot has already started, so the booking can no longer be cancelled.");
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Theory]
    [InlineData(BookingStatus.Attended)]
    [InlineData(BookingStatus.NoShow)]
    public void TransitionTo_OwnerMarksAfterSlotStarts_ShouldSetNewStatus(BookingStatus newStatus)
    {
        // Arrange
        var booking = NewConfirmedBooking();

        // Act
        booking.TransitionTo(newStatus, Actor.Owner, null, FiveMinutesAfterStart, slotHasStarted: true);

        // Assert
        booking.Status.Should().Be(newStatus);
    }

    [Theory]
    [InlineData(BookingStatus.Attended)]
    [InlineData(BookingStatus.NoShow)]
    public void TransitionTo_OwnerMarksBeforeSlotStarts_ShouldThrowConflictException(BookingStatus newStatus)
    {
        // Arrange
        var booking = NewConfirmedBooking();

        // Act
        var act = () => booking.TransitionTo(newStatus, Actor.Owner, null, OneHourBeforeStart, slotHasStarted: false);

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage($"The slot has not started yet, so the booking cannot be marked as {newStatus}.");
    }

    [Theory]
    [InlineData(BookingStatus.Attended)]
    [InlineData(BookingStatus.NoShow)]
    public void TransitionTo_RenterMarksAttendedOrNoShow_ShouldThrowConflictException(BookingStatus newStatus)
    {
        // Arrange
        var booking = NewConfirmedBooking();

        // Act
        var act = () => booking.TransitionTo(newStatus, Actor.Renter, null, FiveMinutesAfterStart, slotHasStarted: true);

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage($"Only the owner can mark a booking as {newStatus}.");
    }

    [Theory]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Attended)]
    [InlineData(BookingStatus.NoShow)]
    public void TransitionTo_FromFinalState_ShouldThrowConflictExceptionForEveryNewState(BookingStatus finalStatus)
    {
        // Arrange: Cancelled is reached before the start; Attended and NoShow after the start.
        var booking = NewConfirmedBooking();
        var finalStatusIsCancelled = finalStatus == BookingStatus.Cancelled;
        booking.TransitionTo(finalStatus, Actor.Owner, null,
            finalStatusIsCancelled ? OneHourBeforeStart : FiveMinutesAfterStart, slotHasStarted: !finalStatusIsCancelled);

        foreach (var next in Enum.GetValues<BookingStatus>())
        {
            // Act: try every next state, at a time when it would otherwise be allowed.
            var nextIsCancelled = next == BookingStatus.Cancelled;
            var act = () => booking.TransitionTo(next, Actor.Owner, null,
                nextIsCancelled ? OneHourBeforeStart : FiveMinutesAfterStart, slotHasStarted: !nextIsCancelled);

            // Assert
            act.Should().Throw<ConflictException>()
                .WithMessage($"The booking is already {finalStatus}, which is a final state.*");
        }

        booking.Status.Should().Be(finalStatus);
    }

    private Booking NewConfirmedBooking() =>
        Booking.Create(
            slotId: _fixture.Create<int>(),
            renterEmail: "asha@mail.com",
            partySize: 3,
            nowUtc: SlotStartsAtUtc.AddDays(-1));
}
