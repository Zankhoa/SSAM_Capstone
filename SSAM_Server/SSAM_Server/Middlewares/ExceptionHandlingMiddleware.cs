using System.Net;
using System.Text.Json;
using SSAM.Application.Common.Exceptions;
using SSAM.Application.Common.Models;

namespace SSAM_Server.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning("AppException [{Code}]: {Message}", ex.Code, ex.Message);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = ex.StatusCode;

            var errorResponse = new ErrorResponseDto(ex.Code, ex.Message, ex.Errors);
            var responseJson = JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var errorResponse = new ErrorResponseDto("INTERNAL_SERVER_ERROR", "Đã xảy ra lỗi trên hệ thống.");
            var responseJson = JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(responseJson);
        }
    }
}
