using System.ComponentModel.DataAnnotations;
using HireYar.Api.Security;
using HireYar.Application.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

public sealed class AnswerUpload
{
    public Guid QuestionId { get; set; }
    [Required] public IFormFile Audio { get; set; } = null!;
    public int? DurationSeconds { get; set; }
    [StringLength(128, MinimumLength = 1)] public string? ClientTurnId { get; set; }
    public DateTimeOffset? LastSpeechEndUtc { get; set; }
    public IFormFile? Video { get; set; }
}
public sealed class IntroductionUpload
{
    [Required] public IFormFile Audio { get; set; } = null!;
    public int? DurationSeconds { get; set; }
    [Required, StringLength(128, MinimumLength = 1)] public string ClientTurnId { get; set; } = "";
    public DateTimeOffset? LastSpeechEndUtc { get; set; }
    public IFormFile? Video { get; set; }
}
[ApiController, Route("api/candidate/interviews/{interviewId:guid}")]
[Authorize(AuthenticationSchemes = InterviewAuthenticationHandler.SchemeName)]
[EnableRateLimiting("candidate")]
public sealed class CandidateInterviewsController(CandidateInterviewService service, InterviewAccessStatusService accessStatus) : ControllerBase
{
    [HttpGet("access-status"), AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<InterviewAccessStatus> AccessStatus(Guid interviewId, CancellationToken ct) => accessStatus.GetAsync(interviewId, ct);
    [HttpPost("start")] public Task<CandidateInterviewDto> Start(Guid interviewId, CancellationToken ct) => service.StartAsync(interviewId, ct);
    [HttpGet("current")] public Task<CandidateInterviewDto> Current(Guid interviewId, CancellationToken ct) => service.CurrentAsync(interviewId, ct);
    [HttpPost("stage/advance")] public Task<CandidateInterviewDto> AdvanceStage(Guid interviewId, AdvanceInterviewStageRequest r, CancellationToken ct) => service.AdvanceStageAsync(interviewId, r, ct);
    [HttpGet("messages")] public Task<IReadOnlyList<CandidateMessageDto>> Messages(Guid interviewId, CancellationToken ct) => service.MessagesAsync(interviewId, ct);
    [HttpPost("introduction"), Consumes("multipart/form-data"), RequestSizeLimit(92 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 92 * 1024 * 1024)]
    public async Task<CandidateInterviewDto> Introduction(Guid interviewId, [FromForm] IntroductionUpload upload, CancellationToken ct)
    {
        await using var stream = upload.Audio.OpenReadStream();
        await using var video = upload.Video?.OpenReadStream();
        return await service.SubmitIntroductionAsync(interviewId, stream, upload.Audio.FileName, upload.Audio.ContentType, upload.Audio.Length,
            upload.DurationSeconds, new(upload.ClientTurnId, upload.LastSpeechEndUtc, video, upload.Video?.FileName, upload.Video?.ContentType, upload.Video?.Length ?? 0), ct);
    }
    [HttpPost("introduction/{introductionId:guid}/retry")]
    public Task<CandidateInterviewDto> RetryIntroduction(Guid interviewId, Guid introductionId, CancellationToken ct) => service.RetryIntroductionAsync(interviewId, introductionId, ct);
    [HttpGet("stages/{stage}/audio")]
    public async Task<IActionResult> StageAudio(Guid interviewId, string stage, CancellationToken ct) => File(await service.OpenStageAudioAsync(interviewId, stage, ct), "audio/mpeg", enableRangeProcessing: true);
    [HttpPost("answers"), Consumes("multipart/form-data"), RequestSizeLimit(92 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 92 * 1024 * 1024)]
    public async Task<CandidateInterviewDto> Answer(Guid interviewId, [FromForm] AnswerUpload upload, CancellationToken ct)
    {
        await using var stream = upload.Audio.OpenReadStream();
        await using var video = upload.Video?.OpenReadStream();
        return await service.SubmitAsync(interviewId, upload.QuestionId, stream, upload.Audio.FileName, upload.Audio.ContentType, upload.Audio.Length, upload.DurationSeconds, ct,
            new(upload.ClientTurnId, upload.LastSpeechEndUtc, video, upload.Video?.FileName, upload.Video?.ContentType, upload.Video?.Length ?? 0));
    }
    [HttpPost("answers/{answerId:guid}/retry")] public Task<CandidateInterviewDto> Retry(Guid interviewId, Guid answerId, CancellationToken ct) => service.RetryAsync(interviewId, answerId, ct);
    [HttpPost("complete")] public Task<CandidateInterviewDto> Complete(Guid interviewId, CancellationToken ct) => service.CompleteAsync(interviewId, ct);
    [HttpGet("questions/{questionId:guid}/audio")]
    public async Task<IActionResult> Audio(Guid interviewId, Guid questionId, CancellationToken ct) => File(await service.OpenQuestionAudioAsync(interviewId, questionId, ct), "audio/mpeg", enableRangeProcessing: true);
}
