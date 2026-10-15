using Amazon.Runtime;
using Amazon.S3;
using HireYar.Application.Interviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Interviews;

public static class InterviewDependencyInjection
{
    public static IServiceCollection AddInterviews(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(config.GetSection("InterviewDiagnostics").Get<InterviewDiagnosticOptions>() ?? new());
        services.AddSingleton<IInterviewDiagnostics, InterviewDiagnostics>();
        services.AddOptions<SpeechEncodingOptions>().Bind(config.GetSection("SpeechEncoding"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.FfmpegPath) && o.TimeoutSeconds is >= 1 and <= 120, "Invalid speech encoder configuration.").ValidateOnStart();
        services.AddSingleton<IPcmSpeechEncoder, FfmpegPcmSpeechEncoder>();
        var provider = config["AI:Provider"] ?? "Disabled";
        if (provider is not ("NetArz" or "Liara" or "Disabled"))
            throw new InvalidOperationException("AI:Provider must be NetArz, Liara or Disabled.");
        switch (provider)
        {
            case "NetArz":
                ConfigureProvider<NetArzOptions>(services, config, provider);
                services.AddHttpClient<NetArzClient>(h => h.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers();
                RegisterProvider<NetArzClient>(services);
                break;
            case "Liara":
                ConfigureProvider<LiaraOptions>(services, config, provider);
                if (OpenAiCompatibleInterviewClient.RequiresPcmEncoding(provider, config["AI:Liara:TtsModel"] ?? new LiaraOptions().TtsModel))
                    services.AddHostedService<GeminiSpeechStartupCheck>();
                services.AddHttpClient<LiaraClient>(h => h.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers();
                RegisterProvider<LiaraClient>(services);
                break;
            default:
                services.AddScoped<DisabledInterviewProvider>();
                RegisterProvider<DisabledInterviewProvider>(services);
                break;
        }
        var timeoutSection = config.GetSection($"AI:{provider}");
        services.AddOptions<InterviewOptions>().Bind(config.GetSection("InterviewAudio"))
            .Validate(o => o.MaxUploadBytes is >= 1024 and <= 52428800 && o.MaxVideoUploadBytes is >= 1024 and <= 41943040 && o.MaxAnswerSeconds is >= 1 and <= 900 && o.ShortlistLimit is >= 5 and <= 20 && o.LeaseSeconds is >= 60 and <= 900,
                "Invalid interview limits.")
            .Validate(o => o.MaxRecoveryAttempts is >= 1 and <= 10 && o.RecoveryPollSeconds is >= 1 and <= 300, "Invalid recovery limits.")
            .Validate(o => o.HistoryCharacterBudget is >= 2000 and <= 128000, "Invalid interview history budget.")
            .Validate(o => provider == "Disabled" || o.LeaseSeconds > timeoutSection.GetValue("SttTimeoutSeconds", 60) + timeoutSection.GetValue("LlmTimeoutSeconds", 30) + 30,
                "InterviewAudio:LeaseSeconds must exceed STT + LLM timeouts plus 30 seconds.").ValidateOnStart();
        services.AddSingleton(s => s.GetRequiredService<IOptions<InterviewOptions>>().Value);
        services.AddOptions<InterviewStorageOptions>().Bind(config.GetSection("InterviewStorage"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath) && !o.RootPath.Split('/', '\\').Contains("wwwroot", StringComparer.OrdinalIgnoreCase), "InterviewStorage must be private and outside wwwroot.").ValidateOnStart();
        services.AddScoped<QuestionSuggestionService>();
        services.AddOptions<InterviewInvitationOptions>().Bind(config.GetSection("InterviewInvitations"))
            .Validate(o => o.CandidateLinkPath.StartsWith('/') && !o.CandidateLinkPath.StartsWith("//") &&
                o.CandidateLinkPath.Contains("{id}") && o.CandidateLinkPath.Length <= 200 &&
                !o.CandidateLinkPath.Any(char.IsWhiteSpace) && !o.CandidateLinkPath.Contains('?') && !o.CandidateLinkPath.Contains('#'),
                "InterviewInvitations:CandidateLinkPath must be a relative page path containing {id} without query or fragment.").ValidateOnStart();
        services.AddSingleton<IInterviewInvitationSecurity, ProtectedInterviewInvitationSecurity>();
        services.AddScoped<SessionReportService>();
        services.AddScoped<SessionReportPolicyService>();
        services.AddScoped<SessionReportDecisionService>();
        services.AddOptions<AnswerAssessmentOptions>().Bind(config.GetSection("AnswerAssessments"))
            .Validate(o => o.PollSeconds is >= 1 and <= 60 && o.AttemptTimeoutSeconds is >= 1 and <= 120,
                "Invalid answer assessment timing.").ValidateOnStart();
        services.AddSingleton(s => s.GetRequiredService<IOptions<AnswerAssessmentOptions>>().Value);
        services.AddScoped<AnswerAssessmentService>();
        services.AddHostedService<AnswerAssessmentWorker>();
        services.AddScoped<InterviewReviewService>();
        services.AddScoped<InterviewAccessStatusService>();
        services.AddSingleton<IResumeTextExtractor, ResumeTextExtractor>();
        services.AddHostedService<InterviewRecoveryWorker>();
        services.AddSingleton<LocalInterviewAudioStorage>();
        services.AddOptions<InterviewStorageOptions>()
            .Validate(o => o.VideoProvider is "Local" or "ArvanS3", "Invalid video storage provider.")
            .Validate(o => o.VideoProvider == "Local" ||
                (Uri.TryCreate(o.S3.Endpoint, UriKind.Absolute, out var u) && u.Scheme == "https" && u.AbsolutePath == "/" &&
                string.IsNullOrEmpty(u.UserInfo) && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment) &&
                !u.Host.StartsWith(o.S3.Bucket + ".", StringComparison.OrdinalIgnoreCase) &&
                System.Text.RegularExpressions.Regex.IsMatch(o.S3.Bucket, "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$") &&
                !string.IsNullOrWhiteSpace(o.S3.Region) && !string.IsNullOrWhiteSpace(o.S3.AccessKey) && !string.IsNullOrWhiteSpace(o.S3.SecretKey)),
                "Private video storage requires an HTTPS service endpoint, bucket, region and credentials.").ValidateOnStart();
        if (config["InterviewStorage:VideoProvider"] == "ArvanS3")
        {
            services.AddSingleton<IAmazonS3>(s => {
                var o = s.GetRequiredService<IOptions<InterviewStorageOptions>>().Value.S3;
                return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), new AmazonS3Config {
                    ServiceURL = o.Endpoint, AuthenticationRegion = o.Region, ForcePathStyle = true,
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                    MaxErrorRetry = 2, Timeout = TimeSpan.FromMinutes(2)
                });
            });
            services.AddSingleton<IInterviewAudioStorage, S3InterviewVideoStorage>();
        }
        else services.AddSingleton<IInterviewAudioStorage>(s => s.GetRequiredService<LocalInterviewAudioStorage>());
        services.AddSingleton<InterviewPolicy>(); services.AddSingleton<InterviewDecisionValidator>();
        services.AddScoped<QuestionAudioService>(); services.AddScoped<CandidateInterviewService>(); services.AddScoped<InterviewManagementService>();
        return services;
    }

    private static void ConfigureProvider<TOptions>(IServiceCollection services, IConfiguration config, string provider)
        where TOptions : class, IOpenAiInterviewOptions
    {
        services.AddOptions<TOptions>().Bind(config.GetSection($"AI:{provider}"))
            .Validate(o => o.Enabled && !string.IsNullOrWhiteSpace(o.ApiKey),
                $"AI:{provider}:ApiKey is required and the selected provider must be enabled.")
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var u) && u.Scheme == "https" &&
                string.IsNullOrEmpty(u.UserInfo) && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment),
                $"AI:{provider}:BaseUrl must be HTTPS.")
            .Validate(o => new[] { o.SttModel, o.LlmModel, o.AnswerAssessmentModel, o.TtsModel, o.TtsVoice }.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100) &&
                o.TtsSpeed is >= 0.25m and <= 4 && o.SttTimeoutSeconds is >= 1 and <= 120 &&
                o.LlmTimeoutSeconds is >= 1 and <= 120 && o.AnswerAssessmentTimeoutSeconds is >= 1 and <= 120 && o.TtsTimeoutSeconds is >= 1 and <= 120,
                "Invalid AI model, voice, speed or timeout configuration.").ValidateOnStart();
    }

    private static void RegisterProvider<TClient>(IServiceCollection services)
        where TClient : class, ISpeechToTextService, IInterviewDecisionService, ITextToSpeechService, IQuestionSuggestionProvider, IAnswerAssessmentProvider
    {
        services.AddScoped<IAnswerAssessmentProvider>(s => s.GetRequiredService<TClient>());
        services.AddScoped<IQuestionSuggestionProvider>(s => s.GetRequiredService<TClient>());
        services.AddScoped<ISpeechToTextService>(s => s.GetRequiredService<TClient>());
        services.AddScoped<IInterviewDecisionService>(s => s.GetRequiredService<TClient>());
        services.AddScoped<ITextToSpeechService>(s => s.GetRequiredService<TClient>());
    }
}
