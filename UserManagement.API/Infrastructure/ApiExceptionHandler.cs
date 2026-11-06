using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace UserManagement.API.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Произошла непредвиденная ошибка. Идентификатор запроса: {TraceId}", httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Внутренняя ошибка сервера.",
            Detail = "Не удалось обработать запрос. Повторите попытку позже.",
            Extensions = { ["traceId"] = httpContext.TraceIdentifier }
        }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);

        return true;
    }
}
