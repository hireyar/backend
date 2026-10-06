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
        if (provider is not ("NetArz" or "Disabled")) throw new InvalidOperationException("AI:Provider must be NetArz or Disabled.");
        services.AddOptions<NetArzOptions>().Bind(config.GetSection("AI:NetArz"))
            .Configure(o => o.Enabled = provider == "NetArz")
            .Validate(o => provider != "NetArz" || !string.IsNullOrWhiteSpace(o.ApiKey), "AI:NetArz:ApiKey is required when AI:Provider=NetArz.")
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var u) && u.Scheme == "https" && string.IsNullOrEmpty(u.UserInfo) && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment), "AI:NetArz:BaseUrl must be HTTPS.")
            .Validate(o => new[] { o.SttModel, o.LlmModel, o.TtsModel, o.TtsVoice }.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100) &&
                o.TtsSpeed is >= 0.25m and <= 4 && o.SttTimeoutSeconds is >= 1 and <= 120 && o.LlmTimeoutSeconds is >= 1 and <= 120 && o.TtsTimeoutSeconds is >= 1 and <= 120,
                "Invalid AI model, voice, speed or timeout configuration.").ValidateOnStart();
        services.AddOptions<InterviewOptions>().Bind(config.GetSection("InterviewAudio"))
            .Validate(o => o.MaxUploadBytes is >= 1024 and <= 52428800 && o.MaxVideoUploadBytes is >= 1024 and <= 41943040 && o.MaxAnswerSeconds is >= 1 and <= 900 && o.ShortlistLimit is >= 5 and <= 20 && o.LeaseSeconds is >= 60 and <= 900,
                "Invalid interview limits.")
            .Validate(o => o.LeaseSeconds > config.GetValue("AI:NetArz:SttTimeoutSeconds", 60) + config.GetValue("AI:NetArz:LlmTimeoutSeconds", 30) + 30,
                "InterviewAudio:LeaseSeconds must exceed STT + LLM timeouts plus 30 seconds.").ValidateOnStart();
        services.AddSingleton(s => s.GetRequiredService<IOptions<InterviewOptions>>().Value);
        services.AddOptions<InterviewStorageOptions>().Bind(config.GetSection("InterviewStorage"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath) && !o.RootPath.Split('/', '\\').Contains("wwwroot", StringComparer.OrdinalIgnoreCase), "InterviewStorage must be private and outside wwwroot.").ValidateOnStart();
        services.AddHttpClient<NetArzClient>(h => h.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers();
        services.AddScoped<QuestionSuggestionService>();
        services.AddScoped<IQuestionSuggestionProvider>(s => s.GetRequiredService<NetArzClient>());
        services.AddScoped<ISpeechToTextService>(s => s.GetRequiredService<NetArzClient>());
        services.AddScoped<IInterviewDecisionService>(s => s.GetRequiredService<NetArzClient>());
        services.AddScoped<ITextToSpeechService>(s => s.GetRequiredService<NetArzClient>());
        services.AddSingleton<IInterviewAudioStorage, LocalInterviewAudioStorage>();
        services.AddSingleton<InterviewPolicy>(); services.AddSingleton<InterviewDecisionValidator>();
        services.AddScoped<QuestionAudioService>(); services.AddScoped<CandidateInterviewService>(); services.AddScoped<InterviewManagementService>();
        return services;
    }
}
