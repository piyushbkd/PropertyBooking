using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PropertyShowingBooking.Api.Contracts.Bookings;
using PropertyShowingBooking.Api.Controllers;
using PropertyShowingBooking.Api.Services.Interfaces;
using System.Net.Mail;

namespace PropertyShowingBooking.Tests.Controllers;

// Unit tests with a mocked IBookingService (Moq): no web server and no database.
public sealed class BookingsControllerUnitTests
{
    private readonly Fixture _fixture = new();

    // Strict: any call the test did not set up fails the test, so the controller cannot do anything unexpected.
    private readonly Mock<IBookingService> _service = new(MockBehavior.Strict);
    private readonly BookingsController _controller;

    public BookingsControllerUnitTests() => _controller = new BookingsController(_service.Object);

    // [MethodUnderTest]_[Scenario]_[ExpectedBehavior]

    [Fact]
    public async Task Book_WithValidRequest_ShouldPassSlotIdAndReturn201WithLocationToGetById()
    {
        // Arrange
        var slotId = _fixture.Create<int>();
        var request = new CreateBookingRequest { RenterEmail = _fixture.Create<MailAddress>().Address, PartySize = 3 };
        var createdBooking = _fixture.Create<BookingResponse>();
        _service.Setup(s => s.BookAsync(slotId, request, It.IsAny<CancellationToken>())).ReturnsAsync(createdBooking);

        // Act
        var result = await _controller.Book(slotId, request, CancellationToken.None);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(BookingsController.GetById));
        created.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(createdBooking.Id);
        created.Value.Should().Be(createdBooking);
        _service.Verify(s => s.BookAsync(slotId, request, It.IsAny<CancellationToken>()), Times.Once);
        _service.VerifyNoOtherCalls();
    }
}
