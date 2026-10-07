using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Interviews;

public sealed class LiaraClient(HttpClient http, IOptions<LiaraOptions> options, ILogger<LiaraClient> logger)
    : OpenAiCompatibleInterviewClient(http, options.Value, logger, "Liara");