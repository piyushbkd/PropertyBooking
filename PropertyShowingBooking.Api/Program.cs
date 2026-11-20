using Microsoft.EntityFrameworkCore;
using PropertyShowingBooking.Api.Data;
using PropertyShowingBooking.Api.Errors;
using PropertyShowingBooking.Api.Services;
using PropertyShowingBooking.Api.Services.Interfaces;
using System.Diagnostics;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Database: SQLite, one file. The connection string is in appsettings.json.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Showings")));

// The current time comes from TimeProvider, so tests can replace it with a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// Services: the use cases. One instance per request, like the database context they use.
// Registered by interface, so the controllers depend on ISlotService / IBookingService, not on the classes.
builder.Services.AddScoped<ISlotService, SlotService>();
builder.Services.AddScoped<IBookingService, BookingService>();

// Errors: every error response uses the standard ProblemDetails JSON format.
// Our BadRequest, NotFound and Conflict exceptions become 400, 404 and 409.
builder.Services.AddProblemDetails(options =>
    // Add a traceId to every error, the same way ASP.NET Core already does for validation errors,
    // so any error a caller reports can be found in the logs.
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd(
            "traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers(options =>
        // Without this, a body that cannot be read also gets a confusing extra
        // "The request field is required." error next to the real one.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
    {
        // Booking states are sent and returned as words ("Cancelled"), not numbers.
        // An unknown word or a number gives 400.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));

        // For unreadable JSON, name the field that is wrong (for example "$.to") but do not show
        // internal .NET type names in the message.
        options.AllowInputFormatterExceptionMessages = false;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Create the database file and tables on first run, and apply any new migrations on later runs,
// so nobody has to run a separate database command before starting the API.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.

// Catches exceptions thrown while handling a request and turns them into ProblemDetails responses.
app.UseExceptionHandler();
// Gives empty error responses (for example 404 for an unknown URL) the same ProblemDetails body.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
