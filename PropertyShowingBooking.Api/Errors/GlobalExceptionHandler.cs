using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace PropertyShowingBooking.Api.Errors;

/// <summary>
/// Turns our three expected exceptions into clear error responses in the standard
/// ProblemDetails JSON format, so every error from this API has the same shape:
/// <code>
/// { "title": "Conflict", "status": 409, "detail": "Only 2 seats left; 3 were requested.", ... }
/// </code>
/// Any other exception is a real bug. It is not handled here, so ASP.NET Core returns a plain
/// 500 response without leaking internal details.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // "type" is the standard link that explains each status code (RFC 9110), the same one
        // ASP.NET Core uses for its own errors.
        var (status, title, type) = exception switch
        {
            BadRequestException => (StatusCodes.Status400BadRequest, "Bad request", "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            _ => (0, string.Empty, string.Empty)
        };

        if (status == 0)
        {
            // Not one of ours, so this is a real bug: log it with its stack trace, then let the
            // default handler return a plain 500. (The built-in middleware's own log entry is
            // switched off in appsettings.json, because in .NET 8 it would also log every expected
            // 400, 404 and 409 as a failure.)
            logger.LogError(exception, "Unexpected error while handling {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            return false;
        }

        logger.LogInformation("Request refused with {StatusCode}: {Message}", status, exception.Message);

        var problem = new ProblemDetails
        {
            Type = type,
            Status = status,
            Title = title,
            Detail = exception.Message
        };

        httpContext.Response.StatusCode = status;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });

        if (!written)
        {
            // The ProblemDetails writer refuses when the caller's Accept header does not allow JSON.
            // Swagger UI sends "Accept: text/plain" by default, and the error would then be replaced
            // by a generic 500 body under our 409. An error must always reach the caller, so write
            // the same ProblemDetails JSON directly.
            problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? httpContext.TraceIdentifier);
            await httpContext.Response.WriteAsJsonAsync(
                problem, options: null, contentType: "application/problem+json", cancellationToken);
        }

        return true;
    }
}
