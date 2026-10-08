using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Interviews;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

public sealed partial class PanelWizardTests
{
    [PostgresFact]
    public async Task Invitation_delivery_runs_after_commit_and_resend_uses_new_code_and_link()
    {
        var (hr, job, template) = await Setup();
        var observed = 0;
        f.Invitations.BeforeSend = async notification => {
            await f.Db(async db => {
                var session = await db.InterviewSessions.AsNoTracking().SingleAsync(s => s.Id == notification.Invitation.InterviewId);
                Assert.Equal(await db.Users.Where(u => u.Id == hr.Id).Select(u => u.OrganizationId).SingleAsync(), session.OrganizationId);
                Assert.Equal("candidate@example.test", notification.Email);
                Assert.Equal("09123456789", notification.Phone);
            }); observed++;
        };
        try
        {
            var response = await Invite(hr, job.Id, template.Id, sendEmail: true, sendSms: true);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var issued = (await response.Content.ReadFromJsonAsync<PanelInvitationResult>())!;
            Assert.Equal("Sent", issued.EmailDelivery); Assert.Equal("Failed", issued.SmsDelivery); Assert.Equal(1, observed);
            var resend = await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{issued.Invitation.InterviewId}/resend", new ResendInterviewRequest(3, true, true));
            Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
            var fresh = (await resend.Content.ReadFromJsonAsync<PanelInvitationResult>())!;
            Assert.Equal("Sent", fresh.EmailDelivery); Assert.Equal("Failed", fresh.SmsDelivery); Assert.Equal(2, observed);
            Assert.NotEqual(issued.Invitation.InterviewId, fresh.Invitation.InterviewId);
            Assert.NotEqual(issued.Invitation.AccessCode, fresh.Invitation.AccessCode);
            Assert.Equal(fresh.Invitation.InterviewUrl, f.Invitations.Last!.Invitation.InterviewUrl);
            await f.Db(async db => Assert.Equal(fresh.Invitation.InterviewId,
                (await db.InterviewSessions.SingleAsync(s => s.Id == issued.Invitation.InterviewId)).SupersededByInterviewId));
        }
        finally { f.Invitations.BeforeSend = null; }
    }
}
