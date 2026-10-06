using HireYar.Application.Common;
using HireYar.Application.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Authorize(Roles = "Owner")]
public sealed class InterviewsController(InterviewManagementService service, IInterviewAudioStorage storage, QuestionAudioService audio, PanelWizardService panel) : ControllerBase
{
    [HttpGet("api/interviews")]
    public Task<Page<PanelInterviewDto>> List([FromQuery] InterviewListQuery query, CancellationToken ct) => panel.ListAsync(query, ct);
    [HttpDelete("api/interviews/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
    [HttpPost("api/interviews/{id:guid}/resend")]
    public Task<PanelInvitationResult> Resend(Guid id, ResendInterviewRequest r, CancellationToken ct) => panel.ResendAsync(id, r, ct);
    [HttpPost("api/interviews/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await service.RevokeAsync(id, ct); return NoContent(); }
    [HttpGet("api/interviews/{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        var result = await panel.ResumeAsync(id, ct);
        return File(result.Content, result.ContentType, "resume" + result.Extension);
    }
    [HttpPost("api/job-positions/{positionId:guid}/applications")]
    public async Task<IActionResult> CreateApplication(Guid positionId, CandidateApplicationRequest r, CancellationToken ct) => Ok(new { id = await service.CreateApplicationAsync(positionId, r, ct) });
    [HttpPost("api/skills")]
    public async Task<IActionResult> CreateSkill(SkillRequest r, CancellationToken ct) => Ok(new { id = await service.CreateSkillAsync(r, ct) });
    [HttpPut("api/job-positions/{positionId:guid}/skills/{skillId:guid}")]
    public async Task<IActionResult> SetSkill(Guid positionId, Guid skillId, PositionSkillRequest r, CancellationToken ct)
    { await service.SetPositionSkillAsync(positionId, skillId, r, ct); return NoContent(); }
    [HttpGet("api/job-positions/{positionId:guid}/question-bank")]
    public Task<IReadOnlyList<BankQuestionDto>> Questions(Guid positionId, CancellationToken ct) => service.QuestionsAsync(positionId, ct);
    [HttpPost("api/job-positions/{positionId:guid}/question-bank")]
    public Task<BankQuestionDto> CreateQuestion(Guid positionId, BankQuestionRequest r, CancellationToken ct) => service.SaveQuestionAsync(positionId, null, r, null, ct);
    [HttpPut("api/job-positions/{positionId:guid}/question-bank/{questionId:guid}")]
    public Task<BankQuestionDto> UpdateQuestion(Guid positionId, Guid questionId, UpdateBankQuestionRequest r, CancellationToken ct) => service.SaveQuestionAsync(positionId, questionId, r.Question, r.RowVersion, ct);
    [HttpPost("api/job-positions/{positionId:guid}/applications/{applicationId:guid}/interviews")]
    public Task<InterviewInvitation> Invite(Guid positionId, Guid applicationId, CreateInterviewRequest r, CancellationToken ct) => service.InviteAsync(positionId, applicationId, r, ct);
    [HttpGet("api/interviews/{id:guid}")]
    public Task<HrInterviewDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost("api/interviews/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct) { await service.RevokeAsync(id, ct); return NoContent(); }
    [HttpGet("api/interviews/{id:guid}/answers/{answerId:guid}/audio")]
    public async Task<IActionResult> AnswerAudio(Guid id, Guid answerId, CancellationToken ct)
    {
        var s = await service.MediaScopedAsync(id, ct); var answer = s.Answers.SingleOrDefault(x => x.Id == answerId) ?? throw AppException.NotFound();
        return File(await storage.OpenReadAsync(answer.AudioStorageKey, ct), answer.AudioContentType, enableRangeProcessing: true);
    }
    [HttpGet("api/interviews/{id:guid}/questions/{questionId:guid}/audio")]
    public async Task<IActionResult> QuestionAudio(Guid id, Guid questionId, CancellationToken ct)
    {
        var s = await service.MediaScopedAsync(id, ct); var q = s.Questions.SingleOrDefault(x => x.QuestionId == questionId) ?? throw AppException.NotFound();
        var cached = await audio.ResolveAsync(s.OrganizationId, q.Text, false, ct) ?? throw AppException.NotFound();
        return File(await storage.OpenReadAsync(cached.AudioStorageKey, ct), "audio/mpeg", enableRangeProcessing: true);
    }
}
