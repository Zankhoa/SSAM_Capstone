using SSAM_Server.DTOs;

namespace SSAM_Server.Common;

public class ServiceResult : ServiceResultDto
{
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

    public static ServiceResult NotFound(string code, string message) =>
        Failure(StatusCodes.Status404NotFound, code, message);

    public static ServiceResult Conflict(string code, string message, Dictionary<string, string[]>? errors = null) =>
        Failure(StatusCodes.Status409Conflict, code, message, errors);

    public static ServiceResult VersionConflict(object current, string message = "Người khác vừa lưu trang này.") =>
        new()
        {
            IsSuccess = false,
            StatusCode = StatusCodes.Status409Conflict,
            ErrorCode = "VERSION_CONFLICT",
            ErrorMessage = message,
            Current = current
        };

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
