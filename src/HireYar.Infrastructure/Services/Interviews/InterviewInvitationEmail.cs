using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using HireYar.Application.Interviews;

namespace HireYar.Infrastructure.Interviews;

internal static partial class InterviewInvitationEmail
{
    private const string ResourcePrefix = "HireYar.Infrastructure.Interviews.EmailTemplates.";
    private static readonly string Template = ReadTemplate();
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    public static void Populate(MailMessage message, InvitationNotification notice)
    {
        var url = notice.Invitation.InterviewUrl;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new FormatException("Invalid invitation URL.");

        var expires = TimeZoneInfo.ConvertTime(notice.Invitation.ExpiresAtUtc, Tehran);
        var calendar = new PersianCalendar();
        var date = PersianDigits($"{calendar.GetYear(expires.DateTime):0000}/{calendar.GetMonth(expires.DateTime):00}/{calendar.GetDayOfMonth(expires.DateTime):00}");
        var time = PersianDigits(expires.ToString("HH:mm", CultureInfo.InvariantCulture));
        var values = new Dictionary<string, string>
        {
            ["CandidateName"] = notice.CandidateName,
            ["PositionTitle"] = notice.PositionTitle,
            ["InterviewUrl"] = url,
            ["AccessCode"] = notice.Invitation.AccessCode ?? "",
            ["ExpiryDate"] = date,
            ["ExpiryTime"] = time
        };
        var html = Placeholder().Replace(Template, match => WebUtility.HtmlEncode(values[match.Groups[1].Value]));
        message.Body = $"{notice.CandidateName} عزیز،\nبرای مصاحبه موقعیت «{notice.PositionTitle}» دعوت شده‌اید.\n\nبرای ورود به مصاحبه، لینک زیر را باز کنید و کد ورود را وارد کنید:\n{url}\n\nکد ورود: {notice.Invitation.AccessCode}\nمهلت ورود: {date}، ساعت {time} (به وقت تهران)\n\nلینک و کد ورود اختصاصی شماست؛ آن‌ها را در اختیار دیگران قرار ندهید.\nهایریار | HireYar\n";
        message.IsBodyHtml = false;
        var view = AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html);
        message.AlternateViews.Add(view);
        var logoStream = typeof(InterviewInvitationEmail).Assembly.GetManifestResourceStream(ResourcePrefix + "hireyar-logo.png")
            ?? throw new InvalidOperationException("Invitation email logo is missing.");
        var logo = new LinkedResource(logoStream, "image/png")
        {
            ContentId = "hireyar-logo",
            TransferEncoding = TransferEncoding.Base64
        };
        view.LinkedResources.Add(logo);
    }

    private static string ReadTemplate()
    {
        using var stream = typeof(InterviewInvitationEmail).Assembly.GetManifestResourceStream(ResourcePrefix + "Invitation.html")
            ?? throw new InvalidOperationException("Invitation email template is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string PersianDigits(string value) => string.Concat(value.Select(c => c is >= '0' and <= '9' ? (char)('۰' + c - '0') : c));

    [GeneratedRegex(@"\{\{(CandidateName|PositionTitle|InterviewUrl|AccessCode|ExpiryDate|ExpiryTime)\}\}")]
    private static partial Regex Placeholder();
}
