using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SSAM_Server.Common.Options;
using SSAM_Server.Repositories;
using SSAM_Server.Security;
using SSAM_Server.Services;

namespace SSAM_Server.Configurations;

/// <summary>
/// Extension methods for registering strongly typed application options and services.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Binds and validates application configuration option sections.
    /// </summary>
    public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ClientAppOptions>()
            .Bind(configuration.GetSection(ClientAppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        return services;
    }

    /// <summary>
    /// Registers repositories, security helpers, domain services, email background pipeline, and AI client services.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUser, CurrentUser>();

        // Repositories
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IPromptRepository, PromptRepository>();

        // Security
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IPermissionService, PermissionService>();

        // Shared domain guards & cross-module sync
        services.AddScoped<IProjectAccessGuard, ProjectAccessGuard>();
        services.AddScoped<IRequirementSyncService, RequirementSyncService>();

        // Domain services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IKanbanService, KanbanService>();
        services.AddScoped<ITestingService, TestingService>();
        services.AddScoped<IWorkService, WorkService>();

        // Email pipeline
        services.AddScoped<SmtpEmailService>();
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailService, QueuedEmailService>();
        services.AddHostedService<EmailDispatcherService>();

        // AI client & service
        var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        if (!string.Equals(aiOptions.Provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("AI:Provider must be set to 'Anthropic' for the configured AI client.");
        }

        services.AddChatClient(_ =>
        {
            var client = new AnthropicClient
            {
                ApiKey = aiOptions.ApiKey,
                BaseUrl = string.IsNullOrWhiteSpace(aiOptions.Endpoint)
                    ? "https://api.anthropic.com"
                    : aiOptions.Endpoint
            };

            return client.AsIChatClient(aiOptions.DefaultModel);
        })
        .UseFunctionInvocation(); // Enables automatic execution of ExecuteSqlQuery tool calls

        services.AddScoped<AiService>();
        services.AddScoped<IAiService>(sp => sp.GetRequiredService<AiService>());

        return services;
    }
}
