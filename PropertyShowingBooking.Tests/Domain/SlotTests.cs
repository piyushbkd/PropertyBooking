using AutoFixture;
using FluentAssertions;
using PropertyShowingBooking.Api.Domain;
using PropertyShowingBooking.Api.Errors;

namespace PropertyShowingBooking.Tests.Domain;

public class SlotTests
{
    private readonly Fixture _fixture = new();

    // [MethodUnderTest]_[Scenario]_[ExpectedBehavior]

    [Fact]
    public void Create_StartTimeIsNow_ShouldThrowBadRequestException()
    {
        // Arrange
        var nowUtc = new DateTime(2026, 11, 21, 4, 30, 0, DateTimeKind.Utc);

        // Act
        var act = () => Slot.Create(_fixture.Create<string>(), startsAtUtc: nowUtc, durationMinutes: 30, capacity: 5, nowUtc);

        // Assert
        act.Should().Throw<BadRequestException>().WithMessage("startsAt must be in the future.");
    }
}
