using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EShop.ServiceDefaults;

// A row still referenced by another table (or a reference to a missing row): PostgreSQL rejects the change
// with 23503, or with 23001 when the foreign key is ON DELETE RESTRICT (cart_items → products).
// Repositories check this beforehand, but a concurrent request can slip in between the check
// and SaveChanges; the constraint has the last word, and the client gets 409 instead of 500.
public sealed class ForeignKeyViolationHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (
            exception
            is not DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.ForeignKeyViolation
                        or PostgresErrorCodes.RestrictViolation
                }
            }
        )
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
                    Title = "The resource is referenced by other data",
                },
            }
        );
    }
}
