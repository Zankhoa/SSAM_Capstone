using Microsoft.AspNetCore.Mvc;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.Common;

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
                    result.WorkflowIssues,
                    result.Current,
                    result.UnmetCriteria));
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
                    result.WorkflowIssues,
                    result.Current,
                    result.UnmetCriteria));
        }

        return controller.StatusCode(result.StatusCode, result.Data);
    }
}
