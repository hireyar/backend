using HireYar.Application.Billing;
using HireYar.Domain.Billing;
using HireYar.Infrastructure.Billing;
using HireYar.Infrastructure.Interviews;
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
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Default"), n => n.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddScoped<IApplicationDbContext>(s => s.GetRequiredService<AppDbContext>());
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = false; o.Password.RequiredLength = 12; o.Password.RequireNonAlphanumeric = true;
            o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.SignIn.RequireConfirmedEmail = true;
        }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));
        services.Configure<SecurityOptions>(config.GetSection("Security"));
        services.AddOptions<KavenegarOptions>().Bind(config.GetSection("Kavenegar"))
            .Validate(o => !o.UseFixedOtp || (o.FixedOtpCode.Length == 6 && o.FixedOtpCode.All(char.IsAsciiDigit)),
                "Kavenegar:FixedOtpCode must be exactly six ASCII digits when UseFixedOtp is enabled.")
            .ValidateOnStart();
        // The API key is part of Kavenegar's URL; avoid HttpClientFactory request URI logging.
        services.AddSingleton<IPhoneOtpSender>(s => new KavenegarOtpSender(
            new HttpClient { BaseAddress = new Uri("https://api.kavenegar.com/"), Timeout = TimeSpan.FromSeconds(10) },
            s.GetRequiredService<Microsoft.Extensions.Options.IOptions<KavenegarOptions>>()));
        services.AddOptions<SmtpOptions>().Bind(config.GetSection("Smtp")).Validate(s => !string.IsNullOrWhiteSpace(s.Host) && s.Port is > 0 and <= 65535 && System.Net.Mail.MailAddress.TryCreate(s.From, out _), "Smtp Host, Port and From are required.").ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>(); services.AddScoped<IAccounts, Accounts>(); services.AddScoped<ITeamMembers, TeamMembers>();
        services.AddScoped<PricingService>(); services.AddScoped<WalletService>(); services.AddScoped<PaymentService>();
        services.AddOptions<BillingPanelOptions>().Bind(config.GetSection("BillingPanel"))
            .Validate(x => x.SubscriptionTaxPercent is >= 0 and <= 100 && BillingMath.HasPrecision(x.SubscriptionTaxPercent, 2), "BillingPanel tax percent must be between 0 and 100 with at most two decimals.").ValidateOnStart();
        services.AddScoped<BillingPanelService>();
        services.AddScoped<ISubscriptionEntitlementService, SubscriptionEntitlementService>(); services.AddScoped<InterviewBillingService>(); services.AddScoped<BillingQueryService>();
        services.AddSingleton<IPaymentGateway, DisabledPaymentGateway>();
        services.AddSingleton(TimeProvider.System); services.AddInterviews(config); return services;
    }
}
