using HireYar.Application.Interviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Interviews;

public static class InterviewDependencyInjection
{
    public static IServiceCollection AddInterviews(this IServiceCollection services, IConfiguration config)
    {
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
            .Validate(o => provider == "Disabled" || o.LeaseSeconds > timeoutSection.GetValue("SttTimeoutSeconds", 60) + timeoutSection.GetValue("LlmTimeoutSeconds", 30) + 30,
                "InterviewAudio:LeaseSeconds must exceed STT + LLM timeouts plus 30 seconds.").ValidateOnStart();
        services.AddSingleton(s => s.GetRequiredService<IOptions<InterviewOptions>>().Value);
        services.AddOptions<InterviewStorageOptions>().Bind(config.GetSection("InterviewStorage"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath) && !o.RootPath.Split('/', '\\').Contains("wwwroot", StringComparer.OrdinalIgnoreCase), "InterviewStorage must be private and outside wwwroot.").ValidateOnStart();
        services.AddScoped<QuestionSuggestionService>();
        services.AddSingleton<IResumeTextExtractor, ResumeTextExtractor>();
        services.AddHostedService<InterviewRecoveryWorker>();
        services.AddSingleton<IInterviewAudioStorage, LocalInterviewAudioStorage>();
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
            .Validate(o => new[] { o.SttModel, o.LlmModel, o.TtsModel, o.TtsVoice }.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100) &&
                o.TtsSpeed is >= 0.25m and <= 4 && o.SttTimeoutSeconds is >= 1 and <= 120 &&
                o.LlmTimeoutSeconds is >= 1 and <= 120 && o.TtsTimeoutSeconds is >= 1 and <= 120,
                "Invalid AI model, voice, speed or timeout configuration.").ValidateOnStart();
    }

    private static void RegisterProvider<TClient>(IServiceCollection services)
        where TClient : class, ISpeechToTextService, IInterviewDecisionService, ITextToSpeechService, IQuestionSuggestionProvider
    {
        services.AddScoped<IQuestionSuggestionProvider>(s => s.GetRequiredService<TClient>());
        services.AddScoped<ISpeechToTextService>(s => s.GetRequiredService<TClient>());
        services.AddScoped<IInterviewDecisionService>(s => s.GetRequiredService<TClient>());
        services.AddScoped<ITextToSpeechService>(s => s.GetRequiredService<TClient>());
    }
}
