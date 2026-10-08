using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed class FakeInvitationNotifier : IInterviewInvitationNotifier
{
    public InvitationNotification? Last { get; private set; }
    public Func<InvitationNotification, Task>? BeforeSend { get; set; }
    public async Task<InvitationDelivery> SendAsync(InvitationNotification notification, bool sendEmail, bool sendSms, CancellationToken ct)
    {
        Last = notification;
        if (BeforeSend is not null) await BeforeSend(notification);
        return new(sendEmail ? "Sent" : "NotConfigured", sendSms ? "Failed" : "NotConfigured");
    }
}
