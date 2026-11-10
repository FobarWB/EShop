using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace EShop.ServiceDefaults.Keycloak;

// 503 instead of 500: the request was valid, the identity provider is down; the client may try again later.
public sealed class IdentityProviderUnavailableHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is not IdentityProviderUnavailableException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails =
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "The account service is unavailable. Please try again later.",
                },
            }
        );
    }
}
