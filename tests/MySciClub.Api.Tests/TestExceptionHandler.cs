using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace MySciClub.Api.Tests;

public sealed class TestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "text/plain";
        await httpContext.Response.WriteAsync(exception.ToString(), cancellationToken);
        return true;
    }
}
