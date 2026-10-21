using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.Middlewares;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = context.TraceIdentifier;

        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        var (status, body) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                new ErrorResponseDto("CONCURRENCY_CONFLICT", "The resource was modified by another request. Please retry.", null, traceId)),
            DbUpdateException { InnerException: SqlException { Number: SqlUniqueIndexViolation or SqlUniqueConstraintViolation } } => (StatusCodes.Status409Conflict,
                new ErrorResponseDto("CONFLICT", "The resource already exists.", null, traceId)),
            _ => (StatusCodes.Status500InternalServerError,
                new ErrorResponseDto("INTERNAL_SERVER_ERROR", "An unexpected error occurred.", null, traceId))
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);
        else
            _logger.LogWarning("Request failed [{Code}] {Message} TraceId={TraceId}", body.Code, body.Message, traceId);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }
}
