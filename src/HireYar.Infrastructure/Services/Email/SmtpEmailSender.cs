using System.Net;
using System.Net.Mail;
using HireYar.Application.Common;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1025;
    public bool EnableSsl { get; set; }
    public string From { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }
}
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    {
        var s = options.Value;
        using var client = new SmtpClient(s.Host, s.Port) { EnableSsl = s.EnableSsl, UseDefaultCredentials = false, Timeout = 10000 };
        if (!string.IsNullOrEmpty(s.Username)) client.Credentials = new NetworkCredential(s.Username, s.Password);
        using var message = new MailMessage(s.From, recipient, subject, body) { BodyEncoding = System.Text.Encoding.UTF8, SubjectEncoding = System.Text.Encoding.UTF8 };
        await client.SendMailAsync(message, ct);
    }
}
