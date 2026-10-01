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
}
[ApiController, Route("api/candidate/interviews/{interviewId:guid}")]
[Authorize(AuthenticationSchemes = InterviewAuthenticationHandler.SchemeName)]
[EnableRateLimiting("candidate")]
public sealed class CandidateInterviewsController(CandidateInterviewService service) : ControllerBase
{
    [HttpPost("start")] public Task<CandidateInterviewDto> Start(Guid interviewId, CancellationToken ct) => service.StartAsync(interviewId, ct);
    [HttpGet("current")] public Task<CandidateInterviewDto> Current(Guid interviewId, CancellationToken ct) => service.CurrentAsync(interviewId, ct);
    [HttpPost("answers"), RequestSizeLimit(54 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 54 * 1024 * 1024)]
    public async Task<CandidateInterviewDto> Answer(Guid interviewId, [FromForm] AnswerUpload upload, CancellationToken ct)
    {
        await using var stream = upload.Audio.OpenReadStream();
        return await service.SubmitAsync(interviewId, upload.QuestionId, stream, upload.Audio.FileName, upload.Audio.ContentType, upload.Audio.Length, upload.DurationSeconds, ct);
    }
    [HttpPost("answers/{answerId:guid}/retry")] public Task<CandidateInterviewDto> Retry(Guid interviewId, Guid answerId, CancellationToken ct) => service.RetryAsync(interviewId, answerId, ct);
    [HttpPost("complete")] public Task<CandidateInterviewDto> Complete(Guid interviewId, CancellationToken ct) => service.CompleteAsync(interviewId, ct);
    [HttpGet("questions/{questionId:guid}/audio")]
    public async Task<IActionResult> Audio(Guid interviewId, Guid questionId, CancellationToken ct) => File(await service.OpenQuestionAudioAsync(interviewId, questionId, ct), "audio/mpeg", enableRangeProcessing: true);
}
