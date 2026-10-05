using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SSAM.Application.Common.Interfaces;
using SSAM.Application.Common.Interfaces.Repositories;
using SSAM.Application.Common.Interfaces.Security;
using SSAM.Infrastructure.Persistence;
using SSAM.Infrastructure.Persistence.Repositories;
using SSAM.Infrastructure.Security;

using SSAM.Application.Features.Emails;
using SSAM.Infrastructure.Configuration;
using SSAM.Infrastructure.Services;

namespace SSAM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=localhost;Database=SSAM_Db;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, b =>
            {
                b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Register Repository & Unit of Work
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Security Services
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        // Register Email Service
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();

        return services;
    }
}
