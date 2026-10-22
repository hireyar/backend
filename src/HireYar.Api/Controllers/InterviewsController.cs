using HireYar.Application.Common;
using HireYar.Application.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Authorize(Roles = "Owner,Recruiter")]
public sealed class InterviewsController(InterviewManagementService service, IInterviewAudioStorage storage, QuestionAudioService audio, PanelWizardService panel, InterviewReviewService reviews) : ControllerBase
{
    [HttpGet("api/interviews/{id:guid}/review")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<InterviewReviewDto> Review(Guid id, CancellationToken ct) => reviews.GetAsync(id, ct);
    [HttpGet("api/interviews/{id:guid}/recording/segments")]
    public Task<InterviewRecordingProgressDto> RecordingSegments(Guid id, CancellationToken ct) => reviews.RecordingProgressAsync(id, false, ct);
    [HttpGet("api/interviews/{id:guid}/recording/segments/{number:int}/video")]
    public async Task<IActionResult> RecordingSegment(Guid id, int number, CancellationToken ct, [FromQuery] bool download = false)
    {
        var media = await reviews.OpenSegmentAsync(id, number, ct);
        return PrivateVideo(media.Content, media.ContentType, $"recording-{id:N}-{number}", download, media.ContentHash);
    }
    [Authorize]
    [HttpPost("api/interviews/{id:guid}/decisions")]
    public Task<InterviewReviewEntryDto> Decide(Guid id, InterviewHumanDecisionRequest r, CancellationToken ct) => reviews.DecideAsync(id, r, ct);
    [HttpGet("api/interviews/{id:guid}/recording/video")]
    public async Task<IActionResult> Recording(Guid id, CancellationToken ct, [FromQuery] bool download = false)
    {
        var media = await reviews.OpenRecordingAsync(id, ct);
        return PrivateVideo(media.Content, media.ContentType, $"recording-{id:N}", download);
    }
    [HttpGet("api/interviews")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<Page<PanelInterviewDto>> List([FromQuery] InterviewListQuery query, CancellationToken ct) => panel.ListAsync(query, ct);
    [HttpDelete("api/interviews/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
    [HttpPost("api/interviews/{id:guid}/resend")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<PanelInvitationResult> Resend(Guid id, ResendInterviewRequest r, CancellationToken ct) => panel.ResendAsync(id, r, ct);
    [HttpGet("api/interviews/{id:guid}/access")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<InterviewAccessDetails> Access(Guid id, CancellationToken ct) => panel.AccessAsync(id, ct);
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
    public Task<Page<BankQuestionDto>> Questions(Guid positionId, [FromQuery] PageQuery pagination, CancellationToken ct) => service.QuestionsAsync(positionId, pagination, ct);
    [HttpPost("api/job-positions/{positionId:guid}/question-bank")]
    public Task<BankQuestionDto> CreateQuestion(Guid positionId, BankQuestionRequest r, CancellationToken ct) => service.SaveQuestionAsync(positionId, null, r, null, ct);
    [HttpPut("api/job-positions/{positionId:guid}/question-bank/{questionId:guid}")]
    public Task<BankQuestionDto> UpdateQuestion(Guid positionId, Guid questionId, UpdateBankQuestionRequest r, CancellationToken ct) => service.SaveQuestionAsync(positionId, questionId, r.Question, r.RowVersion, ct);
    [HttpPost("api/job-positions/{positionId:guid}/applications/{applicationId:guid}/interviews")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<InterviewInvitation> Invite(Guid positionId, Guid applicationId, CreateInterviewRequest r, CancellationToken ct) => service.InviteAsync(positionId, applicationId, r, ct);
    [HttpGet("api/interviews/{id:guid}")]
    public Task<HrInterviewDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpGet("api/interviews/{id:guid}/introduction/audio")]
    public async Task<IActionResult> IntroductionAudio(Guid id, CancellationToken ct)
    {
        var media = await service.OpenIntroductionMediaAsync(id, false, ct);
        return File(media.Content, media.ContentType, enableRangeProcessing: true);
    }
    [HttpGet("api/interviews/{id:guid}/introduction/video")]
    public async Task<IActionResult> IntroductionVideo(Guid id, CancellationToken ct, [FromQuery] bool download = false)
    {
        var media = await service.OpenIntroductionMediaAsync(id, true, ct);
        return PrivateVideo(media.Content, media.ContentType, $"introduction-{id:N}", download);
    }
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
    [HttpGet("api/interviews/{id:guid}/answers/{answerId:guid}/video")]
    public async Task<IActionResult> AnswerVideo(Guid id, Guid answerId, CancellationToken ct, [FromQuery] bool download = false)
    {
        var s = await service.MediaScopedAsync(id, ct);
        var answer = s.Answers.SingleOrDefault(x => x.Id == answerId) ?? throw AppException.NotFound();
        if (answer.VideoStorageKey is null) throw AppException.NotFound();
        return PrivateVideo(await storage.OpenReadAsync(answer.VideoStorageKey, ct), answer.VideoContentType!, $"answer-{answerId:N}", download);
    }
    private FileStreamResult PrivateVideo(Stream stream, string contentType, string name, bool download, string? contentHash = null)
    {
        var file = File(stream, contentType, enableRangeProcessing: true);
        if (contentHash is not null) file.EntityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue('"' + contentHash + '"');
        else if (stream is IInterviewMediaMetadata metadata && Microsoft.Net.Http.Headers.EntityTagHeaderValue.TryParse(metadata.EntityTag, out var tag))
            file.EntityTag = tag;
        else
        {
            // Historical local files have immutable media identities. A weak tag does
            // not claim a byte-level hash or authorize If-Range with a strong validator.
            var identity = System.Text.Encoding.UTF8.GetBytes(name + ":" + stream.Length);
            file.EntityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue('"' + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(identity)) + '"', isWeak: true);
        }
        var filename = name + (contentType == "video/mp4" ? ".mp4" : ".webm");
        if (download) file.FileDownloadName = filename;
        else Response.Headers.ContentDisposition = $"inline; filename=\"{filename}\"";
        return file;
    }
}
