using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Application.Members;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Email;
using HireYar.Infrastructure.Members;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace HireYar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Default")));
        services.AddScoped<IApplicationDbContext>(s => s.GetRequiredService<AppDbContext>());
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = true; o.Password.RequiredLength = 12; o.Password.RequireNonAlphanumeric = true;
            o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.SignIn.RequireConfirmedEmail = true;
        }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));
        services.Configure<SecurityOptions>(config.GetSection("Security"));
        services.AddOptions<SmtpOptions>().Bind(config.GetSection("Smtp")).Validate(s => !string.IsNullOrWhiteSpace(s.Host) && s.Port is > 0 and <= 65535 && System.Net.Mail.MailAddress.TryCreate(s.From, out _), "Smtp Host, Port and From are required.").ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>(); services.AddScoped<IAccounts, Accounts>(); services.AddScoped<ITeamMembers, TeamMembers>();
        services.AddSingleton(TimeProvider.System); return services;
    }
}
