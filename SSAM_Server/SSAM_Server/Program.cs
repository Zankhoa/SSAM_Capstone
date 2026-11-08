using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SSAM_Server;
using SSAM_Server.Common.Options;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Middlewares;
using SSAM_Server.Repositories;
using SSAM_Server.Security;
using SSAM_Server.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging: include trace/span ids in every log scope so errors can be correlated with client reports.
builder.Logging.Configure(o => o.ActivityTrackingOptions =
    ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

// ---- Controllers + uniform validation error contract
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => string.IsNullOrEmpty(e.Key) ? "body" : char.ToLowerInvariant(e.Key[0]) + e.Key[1..],
                    e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Invalid value." : x.ErrorMessage).ToArray());

            return new BadRequestObjectResult(new ErrorResponseDto(
                "VALIDATION_ERROR", "Data Validation Error", errors, context.HttpContext.TraceIdentifier));
        };
    });
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ---- Database (EF Core)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, b =>
    {
        b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        b.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
    });
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// ---- Options
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ClientAppOptions>()
    .Bind(builder.Configuration.GetSection(ClientAppOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));

// ---- Repositories
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();

// ---- Security & Services
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IKanbanService, KanbanService>();
builder.Services.AddScoped<ITestingService, TestingService>();
builder.Services.AddScoped<IWorkService, WorkService>();

builder.Services.AddScoped<SmtpEmailService>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<IEmailService, QueuedEmailService>();
builder.Services.AddHostedService<EmailDispatcherService>();

// ---- CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Cors:AllowedOrigins must list at least one front-end origin.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ---- Reverse proxy support
if (builder.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// ---- Rate limiting
var authPermits = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 20);
var emailPermits = builder.Configuration.GetValue("RateLimiting:EmailPermitPerMinute", 5);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponseDto("TOO_MANY_REQUESTS", "Too many requests. Please slow down and try again shortly.",
                null, context.HttpContext.TraceIdentifier), token);
    };

    static RateLimitPartition<string> PerIp(HttpContext http, string policy, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{policy}:{http.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

    options.AddPolicy(RateLimitPolicies.Auth, http => PerIp(http, RateLimitPolicies.Auth, authPermits));
    options.AddPolicy(RateLimitPolicies.Email, http => PerIp(http, RateLimitPolicies.Email, emailPermits));
});

// ---- JWT authentication
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
try
{
    Validator.ValidateObject(jwt, new ValidationContext(jwt), validateAllProperties: true);
}
catch (ValidationException ex)
{
    throw new InvalidOperationException(
        $"Invalid '{JwtSettings.SectionName}' configuration ({ex.Message}). " +
        "Set JwtSettings:Secret (>= 32 chars) via user-secrets or the JwtSettings__Secret environment variable.", ex);
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ErrorResponseDto(
                "UNAUTHORIZED", "Authentication is required or the token is invalid.", null, context.HttpContext.TraceIdentifier));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ErrorResponseDto(
                "FORBIDDEN", "You do not have permission to perform this action.", null, context.HttpContext.TraceIdentifier));
        }
    };
});

builder.Services.AddAuthorization();

// ---- Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });

var app = builder.Build();

// Auto-apply EF migrations and ensure idempotent schema adjustments for URD doc_type & wider req_uid / anchor_id
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }

    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DocumentNodes_DocType')
BEGIN
    ALTER TABLE [DocumentNodes] DROP CONSTRAINT [CK_DocumentNodes_DocType];
END
ALTER TABLE [DocumentNodes] WITH CHECK ADD CONSTRAINT [CK_DocumentNodes_DocType]
    CHECK (([kind] = 'FOLDER' AND [doc_type] IS NULL) OR ([kind] = 'PAGE' AND [doc_type] IN ('URD','BRD','SRS','ADR','UI_SPEC','TEST_PLAN','OTHER')));
");
    }
    catch
    {
        // Ignore if table doesn't exist yet or constraint already updated
    }
}

if (builder.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers["Cache-Control"] = "no-store";
        }
        return Task.CompletedTask;
    });
    await next();
});

app.UseCors("AllowFrontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

app.Run();

public partial class Program;
