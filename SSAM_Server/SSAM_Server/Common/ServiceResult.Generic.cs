using SSAM_Server.DTOs;

namespace SSAM_Server.Common;

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private init; }

    public static ServiceResult<T> Ok(T data) =>
        new() { IsSuccess = true, StatusCode = StatusCodes.Status200OK, Data = data };

    public static ServiceResult<T> Created(T data) =>
        new() { IsSuccess = true, StatusCode = StatusCodes.Status201Created, Data = data };

    public static ServiceResult<T> FromError(ServiceResultDto error) =>
        new()
        {
            IsSuccess = false,
            StatusCode = error.StatusCode,
            ErrorCode = error.ErrorCode,
            ErrorMessage = error.ErrorMessage,
            Errors = error.Errors,
            RequiredPermission = error.RequiredPermission,
            WorkflowIssues = error.WorkflowIssues,
            Current = error.Current
        };

    public new static ServiceResult<T> Validation(Dictionary<string, string[]> errors, string message = "Data Validation Error") =>
        Failure(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", message, errors);

    public new static ServiceResult<T> BadRequest(string code, string message) =>
        Failure(StatusCodes.Status400BadRequest, code, message);

    public new static ServiceResult<T> WorkflowInvalid(object workflowIssues, string message = "Cấu hình workflow chưa hợp lệ.") =>
        FromError(ServiceResult.WorkflowInvalid(workflowIssues, message));

    public new static ServiceResult<T> Unauthorized(string message = "Email or password incorrect") =>
        Failure(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", message);

    public new static ServiceResult<T> Forbidden(string code, string message, string? requiredPermission = null) =>
        FromError(ServiceResult.Forbidden(code, message, requiredPermission));

    public new static ServiceResult<T> ForbiddenPermission(string requiredPermission, string? message = null) =>
        FromError(ServiceResult.ForbiddenPermission(requiredPermission, message));

    public new static ServiceResult<T> NotFound(string message = "Not Found") =>
        Failure(StatusCodes.Status404NotFound, "NOT_FOUND", message);

    public new static ServiceResult<T> NotFound(string code, string message) =>
        Failure(StatusCodes.Status404NotFound, code, message);

    public new static ServiceResult<T> Conflict(string code, string message, Dictionary<string, string[]>? errors = null) =>
        Failure(StatusCodes.Status409Conflict, code, message, errors);

    public new static ServiceResult<T> VersionConflict(object current, string message = "Người khác vừa lưu trang này.") =>
        FromError(ServiceResult.VersionConflict(current, message));

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
