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
        var message = string.IsNullOrWhiteSpace(i.Message) ? "" : $"""<div dir="rtl" style="direction:rtl;text-align:right;margin:24px 0;padding:16px;border-right:3px solid #14b8a6;background:#101f30;color:#cbd5e1;font-family:Tahoma,Arial,sans-serif;font-size:14px;line-height:28px">{E(i.Message).Replace("\n", "<br>")}</div>""";
        // Repeat direction, alignment and typography on table cells: email clients may discard head/body styles.
        var html = $"""
<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="color-scheme" content="dark light"><title>دعوت به تیم | هایریار</title></head>
<body dir="rtl" style="direction:rtl;text-align:right;margin:0;padding:0;background:#070e18;font-family:Tahoma,Arial,sans-serif;font-size:14px;line-height:28px;color:#e7eefb;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%"><div style="display:none;max-height:0;overflow:hidden">{E(organization)} · {E(role)}</div>
<table role="presentation" dir="rtl" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="#070e18" style="direction:rtl;width:100%;background:#070e18;font-family:Tahoma,Arial,sans-serif"><tr><td dir="rtl" align="center" style="padding:32px 12px;font-family:Tahoma,Arial,sans-serif">
<table role="presentation" dir="rtl" width="560" cellpadding="0" cellspacing="0" border="0" bgcolor="#0e1a2b" style="direction:rtl;width:100%;max-width:560px;background:#0e1a2b;border:1px solid #263449;border-radius:16px;overflow:hidden;font-family:Tahoma,Arial,sans-serif"><tr><td height="4" bgcolor="#14b8a6" style="height:4px;font-size:0;line-height:4px;background:#14b8a6">&nbsp;</td></tr>
<tr><td dir="rtl" align="right" style="direction:rtl;text-align:right;padding:28px 24px;font-family:Tahoma,Arial,sans-serif;font-size:14px;line-height:28px">
<div dir="rtl" style="direction:rtl;text-align:right;font-family:Tahoma,Arial,sans-serif;font-size:20px;line-height:30px;font-weight:bold;color:#5eead4">هایریار <span dir="ltr" style="direction:ltr;display:inline-block;font-family:Tahoma,Arial,sans-serif;font-size:12px;font-weight:normal;color:#8fa3bd">HireYar</span></div>
<h1 dir="rtl" style="direction:rtl;text-align:right;font-family:Tahoma,Arial,sans-serif;font-size:20px;font-weight:bold;line-height:36px;margin:24px 0 12px;color:#e7eefb">{E(title)}</h1>
<p dir="rtl" style="direction:rtl;text-align:right;font-family:Tahoma,Arial,sans-serif;font-size:14px;line-height:28px;margin:0 0 20px;color:#b9c9e0">{E(i.FirstName)} {E(i.LastName)} عزیز،<br>{E(intro)}<br><strong style="font-family:Tahoma,Arial,sans-serif;font-size:15px;line-height:30px;color:#e7eefb">{E(organization)}</strong></p>
<div dir="rtl" style="direction:rtl;text-align:right;padding:14px 16px;border:1px solid #25433f;border-radius:10px;background:#102a2b;font-family:Tahoma,Arial,sans-serif"><span style="font-family:Tahoma,Arial,sans-serif;font-size:12px;line-height:24px;color:#9fb3cf">{E(label)}</span><div style="margin-top:4px;font-family:Tahoma,Arial,sans-serif;font-size:15px;line-height:30px;color:#5eead4;font-weight:bold">{E(role)}</div></div>{message}
<table role="presentation" dir="rtl" align="right" cellpadding="0" cellspacing="0" border="0" style="direction:rtl;margin:24px 0;font-family:Tahoma,Arial,sans-serif"><tr><td dir="rtl" align="center" bgcolor="#14b8a6" style="direction:rtl;text-align:center;border-radius:8px;font-family:Tahoma,Arial,sans-serif;mso-padding-alt:12px 24px"><a dir="rtl" href="{E(url)}" style="direction:rtl;display:inline-block;padding:12px 24px;font-family:Tahoma,Arial,sans-serif;font-size:14px;line-height:28px;font-weight:bold;color:#03201d;text-decoration:none">{E(button)}</a></td></tr></table>
<div style="clear:both"></div><p dir="rtl" style="direction:rtl;text-align:right;margin:16px 0 12px;font-family:Tahoma,Arial,sans-serif;color:#9fb3cf;font-size:12px;line-height:25px">{E(note)}</p>
<p dir="rtl" style="direction:rtl;text-align:right;margin:0;font-family:Tahoma,Arial,sans-serif;font-size:12px;line-height:25px;color:#9fb3cf">اگر دکمه کار نکرد، از لینک زیر استفاده کنید:</p>
<p dir="ltr" align="left" style="direction:ltr;text-align:left;margin:4px 0 0;font-family:Tahoma,Arial,sans-serif;font-size:12px;line-height:24px;word-break:break-all;overflow-wrap:anywhere"><a dir="ltr" style="direction:ltr;font-family:Tahoma,Arial,sans-serif;font-size:12px;line-height:24px;color:#5eead4" href="{E(url)}">{E(url)}</a></p>
<div dir="rtl" style="direction:rtl;text-align:right;border-top:1px solid #263449;margin-top:24px;padding-top:16px;font-family:Tahoma,Arial,sans-serif;color:#8fa3bd;font-size:12px;line-height:25px">{E(ignore)}<br>هایریار</div></td></tr></table></td></tr></table></body></html>
""";
        return (html, $"{i.FirstName} {i.LastName}\n{intro}: {organization}\n{label}: {role}\n{i.Message}\n{button}: {url}\n{note}\n{ignore}");
    }
}
