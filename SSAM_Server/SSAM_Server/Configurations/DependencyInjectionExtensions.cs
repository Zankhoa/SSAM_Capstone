using Anthropic;
using Google.GenAI;
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
        services.Configure<PlantUmlOptions>(configuration.GetSection(PlantUmlOptions.SectionName));
        services.Configure<FigmaOptions>(configuration.GetSection(FigmaOptions.SectionName));

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
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAiRepository, AiRepository>();
        services.AddScoped<IWorkRepository, WorkRepository>();
        services.AddScoped<IFigmaWatchRepository, FigmaWatchRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IKanbanRepository, KanbanRepository>();
        services.AddScoped<ITestingRepository, TestingRepository>();
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
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IKanbanService, KanbanService>();
        services.AddScoped<ITestingService, TestingService>();
        services.AddScoped<IWorkService, WorkService>();
        services.AddScoped<IDiagramService, DiagramService>();
        services.AddScoped<IFigmaService, FigmaService>();
        services.AddScoped<IFigmaWatchService, FigmaWatchService>();
        services.AddHostedService<FigmaPollingWorker>();
        services.AddMemoryCache();
        services.AddHttpClient("PlantUml");
        services.AddHttpClient("Figma");

        // Email pipeline
        services.AddScoped<SmtpEmailService>();
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailService, QueuedEmailService>();
        services.AddHostedService<EmailDispatcherService>();

        // Realtime notifications (SSE)
        services.AddSingleton<NotificationBroadcaster>();
        services.AddScoped<INotificationService, NotificationService>();

        // AI client & service (supports both Gemini and Anthropic)
        var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        var useGemini = string.Equals(aiOptions.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(aiOptions.DefaultModel)
                && aiOptions.DefaultModel.StartsWith("gemini", StringComparison.OrdinalIgnoreCase));

        services.AddChatClient(_ =>
        {
            if (useGemini)
            {
                var geminiKey = !string.IsNullOrWhiteSpace(aiOptions.ApiKey)
                    ? aiOptions.ApiKey
                    : configuration["GEMINI_API_KEY"];
                var geminiClient = new Client(apiKey: geminiKey);
                var model = string.IsNullOrWhiteSpace(aiOptions.DefaultModel) ? "gemini-2.5-flash" : aiOptions.DefaultModel;
                return geminiClient.AsIChatClient(model);
            }

            if (!string.Equals(aiOptions.Provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("AI:Provider must be 'Anthropic' or 'Gemini'.");
            }

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
        services.AddScoped<IAiProjectContextRetriever, AiProjectContextRetriever>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IAiTaskService, AiTaskService>();

        return services;
    }
}
