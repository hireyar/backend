using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;

namespace HireYar.Application.Interviews;

public sealed record WizardPositionRequest(
    [Required, StringLength(100)] string Title,
    [Required, StringLength(1000)] string Description);
public sealed record WizardTemplateRequest(
    Guid JobPositionId,
    [Required, StringLength(200)] string Title,
    [Required, RegularExpression("^(general|specialized)$")] string InterviewType,
    [MinLength(5), MaxLength(30)] List<string>? Questions,
    [Required, StringLength(2000)] string WelcomeMessage,
    [Required, StringLength(2000)] string GoodbyeMessage,
    [Range(5, 60)] int DurationMinutes = 60,
    [MinLength(5), MaxLength(10)] List<QuestionTopicRequest>? Topics = null,
    bool CandidateIntroductionEnabled = false,
    [StringLength(2000)] string CandidateIntroductionMessage = HireYar.Domain.InterviewTemplates.InterviewTemplate.DefaultCandidateIntroduction,
    bool CompanyIntroductionEnabled = false,
    [StringLength(2000)] string CompanyIntroductionMessage = "") : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (CandidateIntroductionEnabled && string.IsNullOrWhiteSpace(CandidateIntroductionMessage))
            yield return new("متن معرفی کارجو الزامی است.", [nameof(CandidateIntroductionMessage)]);
        if (CompanyIntroductionEnabled && string.IsNullOrWhiteSpace(CompanyIntroductionMessage))
            yield return new("متن معرفی شرکت الزامی است.", [nameof(CompanyIntroductionMessage)]);
        if ((Questions is null) == (Topics is null))
            yield return new("فقط یکی از سؤال‌های معمولی یا موضوع‌های پیشرفته را ارسال کنید.");
        if (Topics is not null && !QuestionTopicRules.IsValid(Topics))
            yield return new("۵ تا ۱۰ موضوع یکتا، هرکدام با سه سؤال آسان، متوسط و سخت، لازم است.", [nameof(Topics)]);
        if (Questions is not null && Questions.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 2000))
            yield return new("متن هر سؤال الزامی و حداکثر ۲۰۰۰ کاراکتر است.", [nameof(Questions)]);
    }
}
public sealed record UpdateWizardTemplateRequest([Required] WizardTemplateRequest Template,
    [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record WizardInvitationRequest(Guid JobPositionId, Guid TemplateId,
    [Required, StringLength(200)] string FirstName,
    [Required, StringLength(200)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, RegularExpression("^09[0-9]{9}$")] string Phone,
    [Range(1, 14)] int DeadlineDays = 3,
    [Required, RegularExpression("^(general|specialized)$")] string InterviewType = "general",
    [StringLength(32000)] string? ResumeText = null);
public sealed record PanelInvitationResult(InterviewInvitation Invitation, string EmailDelivery, string SmsDelivery);
public sealed record ResendInterviewRequest([Range(1, 14)] int DeadlineDays = 3);
public sealed class InterviewListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public int Skip => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);
    [StringLength(256)] public string? Search { get; set; }
    public Guid? JobPositionId { get; set; }
    public string[]? Statuses { get; set; }
    public DateTimeOffset? InvitedFromUtc { get; set; }
    public DateTimeOffset? InvitedToUtc { get; set; }
    public DateTimeOffset? CompletedFromUtc { get; set; }
    public DateTimeOffset? CompletedToUtc { get; set; }
}
public sealed record PanelInterviewDto(Guid Id, string FirstName, string LastName, string Email, string? Phone,
    Guid JobPositionId, string PositionTitle, Guid TemplateId, string TemplateTitle, string InterviewType,
    string Status, DateTimeOffset InvitedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc, int DeadlineDays, decimal? EvaluationPercent,
    string ReportUrl, string? ResumeUrl, bool CanResend, bool CanCancel, bool VideoAvailable,
    string? InterviewUrl = null, string? AccessCode = null, string? AccessStatus = null);
