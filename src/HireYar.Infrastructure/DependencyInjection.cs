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
        services.AddOptions<SuperAdminOptions>().Bind(config.GetSection("SuperAdmin"))
            .Validate(x => x.IsValid(), "SuperAdmin requires a 32 to 128 character URL-safe RandomKey when enabled and AccessMinutes between 1 and 30.")
            .ValidateOnStart();
        services.AddSingleton<IAdminCaptcha, AdminCaptcha>();
        services.AddScoped<AdminAccounts>();
        services.AddScoped<IAdminAccounts>(s => s.GetRequiredService<AdminAccounts>());
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
        services.AddScoped<HireYar.Application.Organizations.ITeamMembers, HireYar.Infrastructure.Organizations.TeamMembers>();
        services.AddOptions<HireYar.Infrastructure.Support.TicketStorageOptions>().Bind(config.GetSection("TicketStorage"))
            .Validate(x => x.IsValid(), "TicketStorage requires a private dedicated ticket bucket, HTTPS service endpoint, region and credentials.");
        services.AddKeyedSingleton<Amazon.S3.IAmazonS3>("ticketing", (sp, _) => {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HireYar.Infrastructure.Support.TicketStorageOptions>>().Value;
            return new Amazon.S3.AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials(o.AccessKey, o.SecretKey), new Amazon.S3.AmazonS3Config {
                ServiceURL = o.Endpoint, AuthenticationRegion = o.Region, ForcePathStyle = true,
                RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED,
                MaxErrorRetry = 1, Timeout = TimeSpan.FromSeconds(30)
            });
        });
        services.AddSingleton<HireYar.Application.Support.ITicketAttachmentStorage, HireYar.Infrastructure.Support.TicketS3Storage>();
        services.AddScoped<HireYar.Application.Subscriptions.CatalogService>();
        services.AddScoped<HireYar.Application.Subscriptions.SubscriptionService>();
        services.AddScoped<HireYar.Application.Subscriptions.SubscriptionOrderService>();
        services.AddScoped<HireYar.Application.Subscriptions.InterviewQuotaService>();
        services.AddScoped<HireYar.Application.Subscriptions.IOrganizationTeamUsage, HireYar.Infrastructure.Organizations.OrganizationTeamUsage>();
        services.AddSingleton(TimeProvider.System); services.AddInterviews(config); return services;
    }
}
