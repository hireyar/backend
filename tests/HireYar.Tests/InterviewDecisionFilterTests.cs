using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

public sealed partial class PanelWizardTests
{
    [PostgresFact]
    public async Task Interview_list_filters_latest_human_choice_before_pagination_and_combines_filters()
    {
        var (hr, job, template) = await Setup();
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var response = await Invite(hr, job.Id, template.Id);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            ids.Add((await response.Content.ReadFromJsonAsync<PanelInvitationResult>())!.Invitation.InterviewId);
        }
        // Complete synthetic sessions through the domain; record decisions through the real HTTP endpoint.
        await f.Db(async db => {
            foreach (var s in await db.InterviewSessions.Where(s => ids.Take(4).Contains(s.Id)).ToListAsync())
            {
                s.Start(s.Questions.First().QuestionId, s.CreatedAtUtc);
                s.Complete(InterviewCompletionReason.Manual, s.CreatedAtUtc);
            }
            await db.SaveChangesAsync();
        });
        async Task Decide(Guid id, string decision)
        {
            Assert.Equal(HttpStatusCode.OK, (await hr.Client.Send(HttpMethod.Post,
                $"/api/interviews/{id}/decisions", new InterviewHumanDecisionRequest(Guid.NewGuid(), decision))).StatusCode);
        }
        await Decide(ids[0], "reject");
        await Decide(ids[1], "advance");
        await Decide(ids[2], "review");
        await Decide(ids[3], "reject");
        // Deterministic same-timestamp ordering: old rejection must not match after the newer acceptance.
        await f.Db(async db => {
            var old = await db.InterviewReviewEntries.SingleAsync(e => e.InterviewSessionId == ids[0]);

            db.InterviewReviewEntries.Add(new InterviewReviewEntry {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), OrganizationId = old.OrganizationId,
                InterviewSessionId = old.InterviewSessionId, RequestId = Guid.NewGuid(), ActorUserId = hr.Id,
                Kind = "advance", RecordedAtUtc = old.RecordedAtUtc
            });
            db.InterviewReviewEntries.Add(new InterviewReviewEntry {
                OrganizationId = old.OrganizationId, InterviewSessionId = old.InterviewSessionId,
                RequestId = Guid.NewGuid(), Kind = "consent_accepted", ClientOccurredAtUtc = old.RecordedAtUtc,
                RecordedAtUtc = old.RecordedAtUtc.AddMinutes(1)
            });
            await db.SaveChangesAsync();
        });
        async Task<Page<PanelInterviewDto>> List(string query) {
            var response = await hr.Client.Send(HttpMethod.Get, "/api/interviews" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<Page<PanelInterviewDto>>())!;
        }
        Assert.Equal(ids[3], Assert.Single((await List("?humanDecisions=reject")).Items).Id);
        var advanced = await List("?humanDecisions=advance&pageSize=1&page=2");
        Assert.Equal(2, advanced.Total); Assert.Single(advanced.Items);
        Assert.Equal("advance", advanced.Items[0].HumanDecision);
        Assert.Equal(2, advanced.PageNumber);
        var review = Assert.Single((await List("?humanDecisions=review")).Items);
        Assert.Equal(ids[2], review.Id); Assert.Equal("review", review.HumanDecision);
        Assert.Equal(3, (await List($"?humanDecisions=advance&humanDecisions=review&statuses=completed&search=Synthetic&jobPositionId={job.Id}")).Total);
        Assert.Equal(0, (await List("?humanDecisions=advance&statuses=pending")).Total);
        Assert.Equal(0, (await List("?humanDecisions=advance&search=does-not-exist")).Total);
        var all = await List(""); Assert.Equal(5, all.Total);
        Assert.Null(all.Items.Single(x => x.Id == ids[4]).HumanDecision);
        Assert.Equal(0, (await List("?humanDecisions=advance&completedToUtc=2000-01-01T00:00:00Z")).Total);
        var stranger = await f.RegisterOwner();
        var foreign = await stranger.Client.Send(HttpMethod.Get, "/api/interviews?humanDecisions=advance");
        Assert.Empty((await foreign.Content.ReadFromJsonAsync<Page<PanelInterviewDto>>())!.Items);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/interviews?humanDecisions=advance")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await hr.Client.Send(HttpMethod.Delete, $"/api/interviews/{ids[0]}")).StatusCode);
        Assert.Equal(1, (await List("?humanDecisions=advance")).Total);
    }

    [PostgresFact]
    public async Task Interview_list_rejects_invalid_human_decision_filter()
    {
        var hr = await f.RegisterOwner();
        foreach (var value in new[] { "unknown", "accepted", "reject&humanDecisions=unknown", "Advance" })
            Assert.Equal(HttpStatusCode.BadRequest,
                (await hr.Client.Send(HttpMethod.Get, "/api/interviews?humanDecisions=" + value)).StatusCode);
    }
}