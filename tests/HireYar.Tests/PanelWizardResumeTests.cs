using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

public sealed partial class PanelWizardTests
{
    [PostgresFact]
    public async Task Invitations_persist_clean_resume_from_explicit_text_and_pdf_with_nul_glyph()
    {
        var (hr, job, template) = await Setup();
        var manual = await Invite(hr, job.Id, template.Id, resumeText: "  سابقه\0 کار\r\n.NET و نیم‌فاصله\0  ");
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        var manualId = (await manual.Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation.InterviewId;
        var extracted = await Invite(hr, job.Id, template.Id, bytes: ResumeTextTests.PdfWithNulGlyph(), resumeText: null);
        Assert.Equal(HttpStatusCode.OK, extracted.StatusCode);
        var extractedId = (await extracted.Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation.InterviewId;
        await f.Db(async db => {
            Assert.Equal("سابقه کار\r\n.NET و نیم‌فاصله", (await db.InterviewSessions.SingleAsync(s => s.Id == manualId)).ResumeTextSnapshot);
            Assert.Equal("AB", (await db.InterviewSessions.SingleAsync(s => s.Id == extractedId)).ResumeTextSnapshot);
        });
    }

    [PostgresFact]
    public async Task Invalid_nul_only_resume_and_nul_in_identity_return_400_without_creating_invitation()
    {
        var (hr, job, template) = await Setup();
        var resume = await Invite(hr, job.Id, template.Id, resumeText: "\0 \t\r\n\0");
        Assert.Equal(HttpStatusCode.BadRequest, resume.StatusCode);
        Assert.Equal("resume_text_required", (await resume.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var identity = await Invite(hr, job.Id, template.Id, firstName: "Synthetic\0Name");
        Assert.Equal(HttpStatusCode.BadRequest, identity.StatusCode);
        await f.Db(async db => {
            var organizationId = (await db.JobPositions.SingleAsync(j => j.Id == job.Id)).OrganizationId;
            Assert.False(await db.InterviewSessions.AnyAsync(s => s.OrganizationId == organizationId));
            Assert.False(await db.CandidateApplications.AnyAsync(s => s.OrganizationId == organizationId));
        });
    }
}
