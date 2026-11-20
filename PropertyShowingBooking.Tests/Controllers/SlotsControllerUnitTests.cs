using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PropertyShowingBooking.Api.Contracts.Slots;
using PropertyShowingBooking.Api.Controllers;
using PropertyShowingBooking.Api.Services.Interfaces;

namespace PropertyShowingBooking.Tests.Controllers;

// Unit tests with a mocked ISlotService (Moq): no web server and no database.
public sealed class SlotsControllerUnitTests
{
    private readonly Fixture _fixture = new();

    // Strict: any call the test did not set up fails the test, so the controller cannot do anything unexpected.
    private readonly Mock<ISlotService> _service = new(MockBehavior.Strict);
    private readonly SlotsController _controller;

    public SlotsControllerUnitTests() => _controller = new SlotsController(_service.Object);

    // [MethodUnderTest]_[Scenario]_[ExpectedBehavior]

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturn201WithLocationToGetById()
    {
        // Arrange
        var request = new CreateSlotRequest
        {
            PropertyId = _fixture.Create<string>(),
            StartsAt = new DateTimeOffset(2026, 12, 5, 10, 0, 0, TimeSpan.FromHours(5.5)),
            DurationMinutes = 30,
            Capacity = 5
        };
        var createdSlot = _fixture.Create<SlotResponse>();
        _service.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(createdSlot);

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(SlotsController.GetById));
        created.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(createdSlot.Id);
        created.Value.Should().Be(createdSlot);
        _service.Verify(s => s.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _service.VerifyNoOtherCalls();
    }
}
