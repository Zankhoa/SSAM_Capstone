namespace SSAM.Application.Common.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public Dictionary<string, string[]>? Errors { get; }

    public AppException(int statusCode, string code, string message, Dictionary<string, string[]>? errors = null) 
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Errors = errors;
    }

    public static AppException Validation(Dictionary<string, string[]> errors, string message = "Data Validation Error") =>
        new(400, "VALIDATION_ERROR", message, errors);

    public static AppException BadRequest(string code, string message) =>
        new(400, code, message);

    public static AppException Unauthorized(string message = "Email or password incorrect") =>
        new(401, "UNAUTHORIZED", message);

    public static AppException Forbidden(string code, string message) =>
        new(403, code, message);

    public static AppException NotFound(string message = "Not Found") =>
        new(404, "NOT_FOUND", message);

    public static AppException Conflict(string code, string message) =>
        new(409, code, message);
}
