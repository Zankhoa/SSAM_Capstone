using Microsoft.Extensions.DependencyInjection;
using SSAM.Application.Features.Auth;
using SSAM.Application.Features.Companies;
using SSAM.Application.Features.Workspaces;

namespace SSAM.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Feature Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
