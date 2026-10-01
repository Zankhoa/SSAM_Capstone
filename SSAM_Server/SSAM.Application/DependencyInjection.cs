using Microsoft.Extensions.DependencyInjection;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.Services;

namespace SSAM.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Application Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
