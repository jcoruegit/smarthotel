using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SmartHotel.API.Common.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (exception is UserFriendlyException userFriendlyException)
        {
            var userProblem = new ProblemDetails
            {
                Status = userFriendlyException.StatusCode,
                Title = "No pudimos procesar tu solicitud.",
                Detail = userFriendlyException.Message
            };

            userProblem.Extensions["traceId"] = traceId;
            httpContext.Response.StatusCode = userFriendlyException.StatusCode;

            await httpContext.Response.WriteAsJsonAsync(userProblem, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);

        var genericProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ocurrio un error inesperado.",
            Detail = "Intenta nuevamente en unos minutos. Si el problema continua, comparti el traceId con soporte."
        };

        genericProblem.Extensions["traceId"] = traceId;
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(genericProblem, cancellationToken);
        return true;
    }
}
