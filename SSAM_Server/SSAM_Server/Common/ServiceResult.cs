using Microsoft.AspNetCore.Mvc;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.Common;

public interface IServiceResult
{
    bool IsSuccess { get; }
    int StatusCode { get; }
    string? ErrorCode { get; }
    string? ErrorMessage { get; }
    Dictionary<string, string[]>? Errors { get; }
    string? RequiredPermission { get; }
    object? WorkflowIssues { get; }
}

public class ServiceResult : IServiceResult
{
    public bool IsSuccess { get; protected init; }
    public int StatusCode { get; protected init; }
    public string? ErrorCode { get; protected init; }
    public string? ErrorMessage { get; protected init; }
    public Dictionary<string, string[]>? Errors { get; protected init; }
    public string? RequiredPermission { get; protected init; }
    public object? WorkflowIssues { get; protected init; }

    public static ServiceResult Ok() => new() { IsSuccess = true, StatusCode = StatusCodes.Status200OK };
    public static ServiceResult NoContent() => new() { IsSuccess = true, StatusCode = StatusCodes.Status204NoContent };
    public static ServiceResult Accepted() => new() { IsSuccess = true, StatusCode = StatusCodes.Status202Accepted };

    public static ServiceResult Validation(Dictionary<string, string[]> errors, string message = "Data Validation Error") =>
        Failure(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", message, errors);

    public static ServiceResult BadRequest(string code, string message) =>
        Failure(StatusCodes.Status400BadRequest, code, message);

    public static ServiceResult WorkflowInvalid(object workflowIssues, string message = "Cấu hình workflow chưa hợp lệ.") =>
        new()
        {
            IsSuccess = false,
            StatusCode = StatusCodes.Status400BadRequest,
            ErrorCode = "WORKFLOW_INVALID",
            ErrorMessage = message,
            WorkflowIssues = workflowIssues
        };

    public static ServiceResult Unauthorized(string message = "Email or password incorrect") =>
        Failure(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", message);

    public static ServiceResult Forbidden(string code, string message, string? requiredPermission = null) =>
        new()
        {
            IsSuccess = false,
            StatusCode = StatusCodes.Status403Forbidden,
            ErrorCode = code,
            ErrorMessage = message,
            RequiredPermission = requiredPermission
        };

    public static ServiceResult ForbiddenPermission(string requiredPermission, string? message = null) =>
        Forbidden(
            "FORBIDDEN",
            message ?? $"Bạn không có quyền thực hiện thao tác này. ({requiredPermission})",
            requiredPermission);

    public static ServiceResult NotFound(string message = "Not Found") =>
        Failure(StatusCodes.Status404NotFound, "NOT_FOUND", message);

    public static ServiceResult Conflict(string code, string message) =>
        Failure(StatusCodes.Status409Conflict, code, message);

    public static ServiceResult Failure(int statusCode, string code, string message, Dictionary<string, string[]>? errors = null) =>
        new()
        {
            IsSuccess = false,
            StatusCode = statusCode,
            ErrorCode = code,
            ErrorMessage = message,
            Errors = errors
        };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private init; }

    public static ServiceResult<T> Ok(T data) =>
        new() { IsSuccess = true, StatusCode = StatusCodes.Status200OK, Data = data };

    public static ServiceResult<T> Created(T data) =>
        new() { IsSuccess = true, StatusCode = StatusCodes.Status201Created, Data = data };

    public static ServiceResult<T> FromError(IServiceResult error) =>
        new()
        {
            IsSuccess = false,
            StatusCode = error.StatusCode,
            ErrorCode = error.ErrorCode,
            ErrorMessage = error.ErrorMessage,
            Errors = error.Errors,
            RequiredPermission = error.RequiredPermission,
            WorkflowIssues = error.WorkflowIssues
        };

    public new static ServiceResult<T> Validation(Dictionary<string, string[]> errors, string message = "Data Validation Error") =>
        Failure(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", message, errors);

    public new static ServiceResult<T> BadRequest(string code, string message) =>
        Failure(StatusCodes.Status400BadRequest, code, message);

    public new static ServiceResult<T> WorkflowInvalid(object workflowIssues, string message = "Cấu hình workflow chưa hợp lệ.") =>
        new()
        {
            IsSuccess = false,
            StatusCode = StatusCodes.Status400BadRequest,
            ErrorCode = "WORKFLOW_INVALID",
            ErrorMessage = message,
            WorkflowIssues = workflowIssues
        };

    public new static ServiceResult<T> Unauthorized(string message = "Email or password incorrect") =>
        Failure(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", message);

    public new static ServiceResult<T> Forbidden(string code, string message, string? requiredPermission = null) =>
        new()
        {
            IsSuccess = false,
            StatusCode = StatusCodes.Status403Forbidden,
            ErrorCode = code,
            ErrorMessage = message,
            RequiredPermission = requiredPermission
        };

    public new static ServiceResult<T> ForbiddenPermission(string requiredPermission, string? message = null) =>
        Forbidden(
            "FORBIDDEN",
            message ?? $"Bạn không có quyền thực hiện thao tác này. ({requiredPermission})",
            requiredPermission);

    public new static ServiceResult<T> NotFound(string message = "Not Found") =>
        Failure(StatusCodes.Status404NotFound, "NOT_FOUND", message);

    public new static ServiceResult<T> Conflict(string code, string message) =>
        Failure(StatusCodes.Status409Conflict, code, message);

    public new static ServiceResult<T> Failure(int statusCode, string code, string message, Dictionary<string, string[]>? errors = null) =>
        new()
        {
            IsSuccess = false,
            StatusCode = statusCode,
            ErrorCode = code,
            ErrorMessage = message,
            Errors = errors
        };
}

public static class ServiceResultExtensions
{
    public static IActionResult ToActionResult(this ServiceResult result, ControllerBase controller)
    {
        if (!result.IsSuccess)
        {
            return controller.StatusCode(
                result.StatusCode,
                new ErrorResponseDto(
                    result.ErrorCode ?? "ERROR",
                    result.ErrorMessage ?? "Request failed.",
                    result.Errors,
                    controller.HttpContext.TraceIdentifier,
                    result.RequiredPermission,
                    result.WorkflowIssues));
        }

        return result.StatusCode switch
        {
            StatusCodes.Status204NoContent => controller.NoContent(),
            StatusCodes.Status202Accepted => controller.Accepted(),
            _ => controller.StatusCode(result.StatusCode)
        };
    }

    public static IActionResult ToActionResult<T>(this ServiceResult<T> result, ControllerBase controller)
    {
        if (!result.IsSuccess)
        {
            return controller.StatusCode(
                result.StatusCode,
                new ErrorResponseDto(
                    result.ErrorCode ?? "ERROR",
                    result.ErrorMessage ?? "Request failed.",
                    result.Errors,
                    controller.HttpContext.TraceIdentifier,
                    result.RequiredPermission,
                    result.WorkflowIssues));
        }

        return controller.StatusCode(result.StatusCode, result.Data);
    }
}
