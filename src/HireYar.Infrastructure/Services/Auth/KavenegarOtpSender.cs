using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using Microsoft.Extensions.Options;

namespace HireYar.Infrastructure.Auth;

public sealed class KavenegarOptions
{
    public string ApiKey { get; set; } = "";
    public string Template { get; set; } = "";
    public bool UseFixedOtp { get; set; }
    public string FixedOtpCode { get; set; } = "";
}

public sealed class KavenegarOtpSender(HttpClient client, IOptions<KavenegarOptions> settings) : IPhoneOtpSender
{
    public async Task SendAsync(string mobile, string code, CancellationToken ct)
    {
        var options = settings.Value;
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.Template))
            throw new InvalidOperationException("Kavenegar API key and verify template are required.");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["receptor"] = mobile, ["token"] = code, ["template"] = options.Template
        });
        using var response = await client.PostAsync($"v1/{Uri.EscapeDataString(options.ApiKey)}/verify/lookup.json", content, ct);
        response.EnsureSuccessStatusCode();
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        if (body is null || !body.RootElement.TryGetProperty("return", out var result) ||
            !result.TryGetProperty("status", out var status) || status.GetInt32() != 200)
            throw new HttpRequestException("Kavenegar rejected the OTP delivery.");
    }
}
