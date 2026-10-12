using HireYar.Application.Billing;
using HireYar.Domain.Billing;
using HireYar.Infrastructure.Billing;
using HireYar.Infrastructure.Interviews;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Infrastructure.Auth;
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
        services.AddSingleton<InterviewDiagnosticCommandInterceptor>();
        services.AddDbContext<AppDbContext>((sp, o) => o.UseNpgsql(config.GetConnectionString("Default"), n => n.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(sp.GetRequiredService<InterviewDiagnosticCommandInterceptor>()));
        services.AddScoped<IApplicationDbContext>(s => s.GetRequiredService<AppDbContext>());
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = false;
            o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.SignIn.RequireConfirmedPhoneNumber = true;
        }).AddEntityFrameworkStores<AppDbContext>();
        services.Configure<SecurityOptions>(config.GetSection("Security"));
        services.Configure<InvitationNotificationOptions>(config.GetSection("InterviewNotifications"));
        services.AddSingleton<HireYar.Application.Interviews.IInterviewInvitationNotifier>(s => new InterviewInvitationNotifier(
            new HttpClient { BaseAddress = new Uri("https://api.kavenegar.com/"), Timeout = TimeSpan.FromSeconds(10) },
            s.GetRequiredService<Microsoft.Extensions.Options.IOptions<InvitationNotificationOptions>>(),
            s.GetRequiredService<Microsoft.Extensions.Options.IOptions<KavenegarOptions>>()));
        services.AddOptions<KavenegarOptions>().Bind(config.GetSection("Kavenegar"))
            .Validate(o => !o.UseFixedOtp || (o.FixedOtpCode.Length == 6 && o.FixedOtpCode.All(char.IsAsciiDigit)),
                "Kavenegar:FixedOtpCode must be exactly six ASCII digits when UseFixedOtp is enabled.")
            .ValidateOnStart();
        // The API key is part of Kavenegar's URL; avoid HttpClientFactory request URI logging.
        services.AddSingleton<IPhoneOtpSender>(s => new KavenegarOtpSender(
            new HttpClient { BaseAddress = new Uri("https://api.kavenegar.com/"), Timeout = TimeSpan.FromSeconds(10) },
            s.GetRequiredService<Microsoft.Extensions.Options.IOptions<KavenegarOptions>>()));
        services.AddScoped<IAccounts, Accounts>();
        services.AddOptions<RegistrationTrialOptions>().Bind(config.GetSection("RegistrationTrial"))
            .Validate(x => x.DurationDays is >= 1 and <= 3650, "RegistrationTrial:DurationDays must be between 1 and 3650.")
            .Validate(x => x.CreditAmountUsd > 0 && x.CreditAmountUsd < 1_000_000_000_000m && BillingMath.HasPrecision(x.CreditAmountUsd, 6),
                "RegistrationTrial:CreditAmountUsd must be positive, below 1000000000000 and have at most six decimals.")
            .ValidateOnStart();
        services.AddScoped<RegistrationTrialService>();
        services.AddScoped<PricingService>(); services.AddScoped<WalletService>(); services.AddScoped<PaymentService>();
        services.AddOptions<BillingPanelOptions>().Bind(config.GetSection("BillingPanel"))
            .Validate(x => x.SubscriptionTaxPercent is >= 0 and <= 100 && BillingMath.HasPrecision(x.SubscriptionTaxPercent, 2), "BillingPanel tax percent must be between 0 and 100 with at most two decimals.").ValidateOnStart();
        services.AddScoped<BillingPanelService>();
        services.AddScoped<ISubscriptionEntitlementService, SubscriptionEntitlementService>(); services.AddScoped<InterviewBillingService>(); services.AddScoped<BillingQueryService>();
        services.AddSingleton<IPaymentGateway, DisabledPaymentGateway>();
        services.AddSingleton(TimeProvider.System); services.AddInterviews(config); return services;
    }
}
