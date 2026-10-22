using HireYar.Application.Interviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireYar.Infrastructure.Interviews;

internal static class AiCapabilityRegistration
{
    public static AiRoutingOptions AddAiCapabilities(this IServiceCollection services, IConfiguration config)
    {
        var routing = config.GetSection("AI").Get<AiRoutingOptions>() ?? new();
        services.AddOptions<AiRoutingOptions>().Bind(config.GetSection("AI"))
            .Validate(o => o.ValidationError() is null, routing.ValidationError() ?? "Invalid AI routing.").ValidateOnStart();
        services.AddHttpClient("InterviewAI", h => h.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers();
        services.AddScoped<ISpeechToTextService>(s => Resolve<ISpeechToTextService>(s, o => o.SpeechToText));
        services.AddScoped<IInterviewDecisionService>(s => Resolve<IInterviewDecisionService>(s, o => o.InterviewDecision));
        services.AddScoped<IQuestionSuggestionProvider>(s => Resolve<IQuestionSuggestionProvider>(s, o => o.QuestionSuggestion));
        services.AddScoped<IAnswerAssessmentProvider>(s => Resolve<IAnswerAssessmentProvider>(s, o => o.AnswerAssessment));
        services.AddScoped<IReportNarrativeProvider>(s => Resolve<IReportNarrativeProvider>(s, o => o.ReportNarrative));
        services.AddScoped<ITextToSpeechService>(s => Resolve<ITextToSpeechService>(s, o => o.TextToSpeech));
        if (OpenAiCompatibleInterviewClient.RequiresPcmEncoding(routing.TextToSpeech.Provider, routing.TextToSpeech.Model))
            services.AddHostedService<GeminiSpeechStartupCheck>();
        return routing;
    }

    private static T Resolve<T>(IServiceProvider services, Func<AiRoutingOptions, AiCapabilityOptions> select) where T : class
    {
        var routing = services.GetRequiredService<IOptions<AiRoutingOptions>>().Value;
        var route = select(routing);
        if (route.Provider == "Disabled") return (T)(object)new DisabledInterviewProvider();
        var connection = routing.Providers[route.Provider];
        if (route.Provider == "Soniox") return (T)(object)new SonioxSpeechToTextService(connection, route, services.GetRequiredService<ILogger<SonioxSpeechToTextService>>());
        var options = new NetArzOptions {
            BaseUrl = route.Provider == "Cloudflare" ? $"https://api.cloudflare.com/client/v4/accounts/{connection.AccountId}/ai/v1" : connection.BaseUrl,
            ApiKey = route.Provider == "Cloudflare" ? connection.ApiToken : connection.ApiKey,
            SttModel = route.Model, LlmModel = route.Model, AnswerAssessmentModel = route.Model, TtsModel = route.Model,
            SttTimeoutSeconds = route.TimeoutSeconds, LlmTimeoutSeconds = route.TimeoutSeconds,
            AnswerAssessmentTimeoutSeconds = route.TimeoutSeconds, TtsTimeoutSeconds = route.TimeoutSeconds,
            TtsVoice = route.Options.Voice, TtsSpeed = route.Options.Speed
        };
        return (T)(object)new CapabilityClient(services.GetRequiredService<IHttpClientFactory>().CreateClient("InterviewAI"), options,
            services.GetRequiredService<ILogger<CapabilityClient>>(), route.Provider, services.GetRequiredService<IPcmSpeechEncoder>(), route.Options.LanguageHints);
    }

    private sealed class CapabilityClient(HttpClient http, IOpenAiInterviewOptions options, ILogger logger, string provider, IPcmSpeechEncoder encoder, string[] languages)
        : OpenAiCompatibleInterviewClient(http, options, logger, provider, encoder, languages);
}
