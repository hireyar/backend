using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Interviews;
using HireYar.Infrastructure.Members;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Organizations;

public sealed class TeamInvitationEmail(AppDbContext db, IOptions<InvitationNotificationOptions> notification, IOptions<SecurityOptions> security)
{
    public async Task<string> SendAsync(MobileTeamInvitation invite, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(invite.Email)) return "NotRequested";
        var o = notification.Value.Email;
        if (!o.Enabled || string.IsNullOrWhiteSpace(o.Host) || !MailAddress.TryCreate(o.FromAddress, out _) ||
            o.Port is < 1 or > 65535 || o.TimeoutSeconds is < 1 or > 60 || (string.IsNullOrEmpty(o.Username) != string.IsNullOrEmpty(o.Password))) return "NotConfigured";
        var org = await db.Organizations.AsNoTracking().Where(x => x.Id == invite.OrganizationId).Select(x => x.Name).SingleAsync(ct);
        var role = await db.AccessRoles.AsNoTracking().Where(x => x.Code == invite.AccessRoleCode).Select(x => x.Name).SingleAsync(ct);
        var url = security.Value.FrontendUrl.TrimEnd('/') + "/team-invitations/" + invite.Id;
        try {
            using var mail = new MailMessage { From = new(o.FromAddress, o.FromName), Subject = "دعوت به تیم شما | هایریار", SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8 };
            mail.To.Add(new MailAddress(invite.Email));
            var content = Render(invite, org, role, url);
            mail.Body = content.Text;
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(content.Html, Encoding.UTF8, MediaTypeNames.Text.Html));
            using var smtp = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl, UseDefaultCredentials = false,
                Credentials = string.IsNullOrEmpty(o.Username) ? null : new NetworkCredential(o.Username, o.Password) };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
            await smtp.SendMailAsync(mail, timeout.Token); return "Sent";
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return "Failed"; }
        catch (Exception e) when (e is SmtpException or FormatException or ArgumentException or InvalidOperationException) { return "Failed"; }
    }
    public static (string Html, string Text) Render(MobileTeamInvitation i, string organization, string role, string url)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        const string title = "همکاری تازه از اینجا شروع می‌شود.";
        const string intro = "برای پیوستن به تیم دعوت شده‌اید";
        const string label = "سطح دسترسی شما";
        const string button = "پذیرش دعوت و ورود";
        const string note = "با شماره موبایلی که برای دعوت شما ثبت شده وارد شوید. کد تأیید به همان شماره ارسال می‌شود. این دعوت از زمان ایجاد تا ۷۲ ساعت معتبر است.";
        const string ignore = "اگر منتظر این دعوت نبودید، می‌توانید این ایمیل را نادیده بگیرید.";
        var message = string.IsNullOrWhiteSpace(i.Message) ? "" : $"""<div style="margin:24px 0;padding:16px;border-right:3px solid #14b8a6;background:#101f30;color:#cbd5e1;line-height:2">{E(i.Message).Replace("\n", "<br>")}</div>""";
        var html = $"""
<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="color-scheme" content="dark light"></head>
<body style="margin:0;background:#070e18;font-family:Tahoma,Arial,sans-serif;color:#e7eefb"><div style="display:none;max-height:0;overflow:hidden">{E(organization)} · {E(role)}</div>
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#070e18"><tr><td align="center" style="padding:40px 16px">
<table role="presentation" width="560" cellpadding="0" cellspacing="0" style="width:100%;max-width:560px;background:#0e1a2b;border:1px solid #263449;border-radius:20px;overflow:hidden"><tr><td style="height:4px;background:#14b8a6"></td></tr>
<tr><td style="padding:32px 28px"><div style="font-size:22px;font-weight:bold;letter-spacing:1px;color:#5eead4" dir="ltr">HireYar<span style="color:#8fa3bd;font-size:12px"> / TEAM</span></div>
<h1 style="font-size:24px;line-height:1.8;margin:28px 0 12px;color:#e7eefb">{E(title)}</h1><p style="font-size:15px;line-height:2;color:#b9c9e0">{E(i.FirstName)} {E(i.LastName)}<br>{E(intro)}<br><strong style="color:#e7eefb">{E(organization)}</strong></p>
<div style="padding:16px 18px;border:1px solid #25433f;border-radius:12px;background:#102a2b"><span style="font-size:12px;color:#9fb3cf">{E(label)}</span><div style="margin-top:8px;color:#5eead4;font-weight:bold">{E(role)}</div></div>{message}
<table role="presentation" cellpadding="0" cellspacing="0" style="margin:28px 0"><tr><td bgcolor="#14b8a6" style="border-radius:10px"><a href="{E(url)}" style="display:inline-block;padding:15px 26px;font-size:15px;font-weight:bold;color:#03201d;text-decoration:none">{E(button)}</a></td></tr></table>
<p style="color:#9fb3cf;font-size:12px;line-height:2">{E(note)}</p><p style="font-size:11px;line-height:1.8;word-break:break-all" dir="ltr"><a style="color:#5eead4" href="{E(url)}">{E(url)}</a></p>
<div style="border-top:1px solid #263449;margin-top:28px;padding-top:20px;color:#7186a2;font-size:11px;line-height:2">{E(ignore)}<br>HireYar · هایریار</div></td></tr></table></td></tr></table></body></html>
""";
        return (html, $"{i.FirstName} {i.LastName}\n{intro}: {organization}\n{label}: {role}\n{i.Message}\n{button}: {url}\n{note}\n{ignore}");
    }
}
