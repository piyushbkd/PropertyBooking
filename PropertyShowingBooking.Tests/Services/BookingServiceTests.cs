using AutoFixture;
using FluentAssertions;
using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Errors;
using PropertyShowingBooking.Tests.TestSupport;
using System.Net.Mail;

namespace PropertyShowingBooking.Tests.Services;

// Real SQLite database and a fake clock; one fresh database per test.
public sealed class BookingServiceTests : IDisposable
{
    private readonly Fixture _fixture = new();
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    // [MethodUnderTest]_[Scenario]_[ExpectedBehavior]

    [Fact]
    public async Task BookAsync_MoreSeatsThanAreLeft_ShouldThrowConflictExceptionAndKeepSeats()
    {
        // Arrange
        var slot = await _db.CreateSlotAsync(capacity: 5);
        await _db.BookAsync(slot.Id, AnyEmail(), 3);

        // Act
        var act = () => _db.BookAsync(slot.Id, AnyEmail(), 3);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*2 left, 3 requested*");
        (await _db.ReadSlotAsync(slot.Id)).SeatsRemaining.Should().Be(2);
        await _db.AssertSeatsAreConsistentAsync(slot.Id);
    }

    [Theory]
    [InlineData("asha@mail.com")]
    [InlineData("ASHA@Mail.com")]
    [InlineData("  asha@mail.com  ")]
    public async Task BookAsync_SameRenterAlreadyHasActiveBooking_ShouldThrowConflictException(string sameRenterAgain)
    {
        // Arrange
        var slot = await _db.CreateSlotAsync(capacity: 10);
        await _db.BookAsync(slot.Id, "asha@mail.com", 2);

        // Act
        var act = () => _db.BookAsync(slot.Id, sameRenterAgain, 1);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already has an active booking*");
        (await _db.ReadSlotAsync(slot.Id)).SeatsRemaining.Should().Be(8);
    }

    [Fact]
    public async Task BookAsync_SlotHasStarted_ShouldThrowConflictException()
    {
        // Arrange
        var slot = await _db.CreateSlotAsync(startsIn: TimeSpan.FromHours(1));
        _db.Clock.Advance(TimeSpan.FromHours(1)); // now exactly the start time

        // Act
        var act = () => _db.BookAsync(slot.Id, AnyEmail(), 1);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("The slot has already started, so it can no longer be booked.");
    }

    [Fact]
    public async Task TransitionAsync_Cancel_ShouldFreeSeatsAndLetSameRenterBookAgain()
    {
        // Arrange
        var slot = await _db.CreateSlotAsync(capacity: 5);
        var renterEmail = AnyEmail();
        var booking = await _db.BookAsync(slot.Id, renterEmail, 3);

        // Act
        var cancelled = await _db.TransitionAsync(booking.Id, BookingStatus.Cancelled, Actor.Renter, _fixture.Create<string>());
        var bookedAgain = await _db.BookAsync(slot.Id, renterEmail, 1);

        // Assert
        cancelled.Status.Should().Be(BookingStatus.Cancelled);
        bookedAgain.Status.Should().Be(BookingStatus.Confirmed);
        (await _db.ReadSlotAsync(slot.Id)).SeatsRemaining.Should().Be(4);
        await _db.AssertSeatsAreConsistentAsync(slot.Id);
    }

    private string AnyEmail() => _fixture.Create<MailAddress>().Address;
}
