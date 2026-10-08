using System.Net;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class InvitationNotificationTests
{
    private static InvitationNotification Notice => new("candidate@example.test", "09123456789", "Synthetic Candidate", "Synthetic job",
        new(Guid.NewGuid(), "123456", DateTimeOffset.UtcNow.AddDays(3), "/candidate", "123456", "https://example.test/interview/synthetic"));

    private sealed class Handler(HttpStatusCode status = HttpStatusCode.OK, string json = "{\"return\":{\"status\":200}}") : HttpMessageHandler
    {
        public int Calls; public string? Body; public string? Path;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Body = await request.Content!.ReadAsStringAsync(ct); Path = request.RequestUri!.AbsolutePath;
            return new(status) { Content = new StringContent(json) };
        }
    }
    private static InterviewInvitationNotifier Sender(Handler handler, InvitationNotificationOptions options, string otpKey = "synthetic-key") =>
        new(new HttpClient(handler) { BaseAddress = new("https://api.kavenegar.com/") }, Options.Create(options),
            Options.Create(new KavenegarOptions { ApiKey = otpKey, Template = "otp-template" }));

    [Theory]
    [InlineData(false, "invite", false, "NotConfigured")]
    [InlineData(false, "invite", true, "NotConfigured")]
    [InlineData(true, "", true, "NotConfigured")]
    [InlineData(true, "invite", false, "NotRequested")]
    [InlineData(true, "invite", true, "Sent")]
    public async Task Sms_requires_global_configuration_and_explicit_selection(bool enabled, string template, bool selected, string expected)
    {
        var handler = new Handler(); var options = new InvitationNotificationOptions { Sms = new() { Enabled = enabled, Template = template } };
        var result = await Sender(handler, options).SendAsync(Notice, false, selected, default);
        Assert.Equal(expected, result.SmsDelivery); Assert.Equal("NotConfigured", result.EmailDelivery);
        Assert.Equal(expected == "Sent" ? 1 : 0, handler.Calls);
        if (handler.Calls > 0)
        {
            Assert.Equal("/v1/synthetic-key/verify/lookup.json", handler.Path);
            Assert.Contains("template=invite", handler.Body); Assert.Contains("token=123456", handler.Body);
            Assert.Contains("token2=https%3A%2F%2Fexample.test%2Finterview%2Fsynthetic", handler.Body);
            Assert.DoesNotContain("otp-template", handler.Body);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"return\":{\"status\":431}}")]
    [InlineData(HttpStatusCode.OK, "not-json")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task Sms_failure_is_reported_without_throwing_or_retry(HttpStatusCode status, string json)
    {
        var handler = new Handler(status, json);
        var result = await Sender(handler, new() { Sms = new() { Enabled = true, Template = "invite", ApiKey = "independent" } }).SendAsync(Notice, false, true, default);
        Assert.Equal("Failed", result.SmsDelivery); Assert.Equal(1, handler.Calls);
        Assert.Equal("/v1/independent/verify/lookup.json", handler.Path);
    }

    [Fact]
    public async Task Missing_key_or_invalid_phone_never_sends()
    {
        var handler = new Handler(); var options = new InvitationNotificationOptions { Sms = new() { Enabled = true, Template = "invite" } };
        Assert.Equal("NotConfigured", (await Sender(handler, options, "").SendAsync(Notice, false, true, default)).SmsDelivery);
        Assert.Equal("Failed", (await Sender(handler, options).SendAsync(Notice with { Phone = null }, false, true, default)).SmsDelivery);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Email_sends_utf8_invitation_to_local_smtp_only()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new System.Text.StringBuilder();
        var server = Task.Run(async () => {
            using var socket = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = socket.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost test");
            while (await reader.ReadLineAsync(deadline.Token) is { } line)
            {
                if (line.StartsWith("DATA"))
                {
                    await writer.WriteLineAsync("354 continue");
                    while (await reader.ReadLineAsync(deadline.Token) is { } data && data != ".") received.AppendLine(data);
                    await writer.WriteLineAsync("250 accepted");
                }
                else if (line.StartsWith("QUIT")) { await writer.WriteLineAsync("221 bye"); break; }
                else await writer.WriteLineAsync("250 OK");
            }
        });
        try
        {
            var handler = new Handler();
            var options = new InvitationNotificationOptions { Email = new() { Enabled = true, Host = "127.0.0.1", Port = port,
                EnableSsl = false, FromAddress = "sender@example.test" } };
            Assert.Equal("NotRequested", (await Sender(handler, options).SendAsync(Notice, false, false, deadline.Token)).EmailDelivery);
            var result = await Sender(handler, options).SendAsync(Notice, true, false, deadline.Token);
            Assert.Equal("Sent", result.EmailDelivery); await server;
            var wire = received.ToString(); Assert.Contains("candidate@example.test", wire);
            Assert.Contains("charset=utf-8", wire, StringComparison.OrdinalIgnoreCase);
            var payload = wire[(wire.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            Assert.Contains("123456", decoded); Assert.Contains("https://example.test/interview/synthetic", decoded);
            Assert.Contains("دعوت شده‌اید", decoded);
            Assert.Equal(0, handler.Calls);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task Email_failure_does_not_prevent_sms()
    {
        // Reserved loopback port with no listener: never contact an external SMTP server.
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var handler = new Handler();
        var result = await Sender(handler, new() { Email = new() { Enabled = true, Host = "127.0.0.1", Port = port,
            EnableSsl = false, FromAddress = "sender@example.test", TimeoutSeconds = 1 }, Sms = new() { Enabled = true, Template = "invite" } }).SendAsync(Notice, true, true, default);
        Assert.Equal("Failed", result.EmailDelivery); Assert.Equal("Sent", result.SmsDelivery);
    }
}
