using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EShop.ServiceDefaults;

// The row changed between reading and writing it: its concurrency token (products.xmin) no longer matches,
// the UPDATE or DELETE affects no rows and EF Core throws. Nothing of that SaveChanges is stored;
// the client gets 409 and can reload the data and try again.
public sealed class ConcurrencyConflictHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails =
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The resource was changed by another request",
                },
            }
        );
    }
}
