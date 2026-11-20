using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyShowingBooking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseBookingStatusAsConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. Marking Bookings.Status as a concurrency token changes how EF Core
            // writes UPDATE statements (it adds "AND Status = <old value>"), not the table itself.
            // This migration only records the change in the model snapshot.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
