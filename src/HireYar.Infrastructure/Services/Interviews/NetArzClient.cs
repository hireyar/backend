using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Interviews;

public sealed class NetArzClient(HttpClient http, IOptions<NetArzOptions> options, ILogger<NetArzClient> logger)
    : OpenAiCompatibleInterviewClient(http, options.Value, logger, "NetArz");