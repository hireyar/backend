using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace HireYar.Infrastructure.Interviews;

public sealed class InvitationNotificationOptions
{
    public InvitationEmailOptions Email { get; set; } = new();
    public InvitationSmsOptions Sms { get; set; } = new();
}
public sealed class InvitationEmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "HireYar";
    public int TimeoutSeconds { get; set; } = 10;
}
public sealed class InvitationSmsOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string Template { get; set; } = "";
}

// No request URI logging: Kavenegar embeds its API key in the path.
public sealed class InterviewInvitationNotifier(HttpClient http, IOptions<InvitationNotificationOptions> settings,
    IOptions<KavenegarOptions> otp) : IInterviewInvitationNotifier
{
    public async Task<InvitationDelivery> SendAsync(InvitationNotification n, bool sendEmail, bool sendSms, CancellationToken ct)
    {
        var o = settings.Value;
        var email = await DeliverAsync(sendEmail, o.Email.Enabled, EmailConfigured(o.Email),
            () => SendEmailAsync(n, o.Email, ct), ct);
        var key = string.IsNullOrWhiteSpace(o.Sms.ApiKey) ? otp.Value.ApiKey : o.Sms.ApiKey;
        var sms = await DeliverAsync(sendSms, o.Sms.Enabled, !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(o.Sms.Template),
            () => SendSmsAsync(n, key, o.Sms.Template, ct), ct);
        return new(email, sms);
    }

    private static bool EmailConfigured(InvitationEmailOptions o) => !string.IsNullOrWhiteSpace(o.Host) &&
        o.Port is >= 1 and <= 65535 && o.TimeoutSeconds is >= 1 and <= 60 &&
        MailAddress.TryCreate(o.FromAddress, out _) && (string.IsNullOrEmpty(o.Username) == string.IsNullOrEmpty(o.Password));

    private static async Task<string> DeliverAsync(bool selected, bool enabled, bool configured, Func<Task> send, CancellationToken ct)
    {
        if (!enabled || !configured) return "NotConfigured";
        if (!selected) return "NotRequested";
        try { await send(); return "Sent"; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return "Failed"; }
        catch (Exception e) when (e is HttpRequestException or SmtpException or JsonException or FormatException or ArgumentException or InvalidOperationException)
        { return "Failed"; }
    }

    private static async Task SendEmailAsync(InvitationNotification n, InvitationEmailOptions o, CancellationToken ct)
    {
        using var message = new MailMessage { From = new MailAddress(o.FromAddress, o.FromName),
            Subject = "دعوت به مصاحبه | هایریار", SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8 };
        InterviewInvitationEmail.Populate(message, n);
        message.To.Add(new MailAddress(n.Email));
        using var smtp = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl, UseDefaultCredentials = false,
            Credentials = string.IsNullOrEmpty(o.Username) ? null : new NetworkCredential(o.Username, o.Password) };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
        await smtp.SendMailAsync(message, timeout.Token);
    }

    private async Task SendSmsAsync(InvitationNotification n, string key, string template, CancellationToken ct)
    {
        if (n.Phone is null || !Regex.IsMatch(n.Phone, "^09[0-9]{9}$")) throw new FormatException();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["receptor"] = n.Phone, ["template"] = template, ["type"] = "sms",
            ["token"] = n.Invitation.AccessCode!, ["token2"] = n.Invitation.InterviewUrl! });
        using var response = await http.PostAsync($"v1/{Uri.EscapeDataString(key)}/verify/lookup.json", form, ct);
        response.EnsureSuccessStatusCode();
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        if (body is null || !body.RootElement.TryGetProperty("return", out var result) ||
            !result.TryGetProperty("status", out var status) || !status.TryGetInt32(out var value) || value != 200)
            throw new HttpRequestException("Invitation SMS was rejected.");
    }
}
