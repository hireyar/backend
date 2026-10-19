using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.InterviewTemplates;

public sealed record QuestionRequest(Guid? Id,
    [Required, StringLength(2000)] string Text,
    [Range(1, 30)] int Order,
    [Required, StringLength(200)] string Competency,
    [Required, StringLength(4000)] string EvaluationGuidance);
public sealed record TemplateRequest(Guid JobPositionId,
    [Required, StringLength(200)] string Title,
    [Range(5, 30)] int DurationMinutes,
    [Range(0, 3)] int MaxFollowUpQuestionsPerQuestion,
    [Required, MinLength(1), MaxLength(30)] List<QuestionRequest> Questions,
    [Required, StringLength(20)] string Language = "fa-IR", bool IsArchived = false,
    bool CandidateIntroductionEnabled = false,
    [StringLength(2000)] string CandidateIntroductionMessage = InterviewTemplate.DefaultCandidateIntroduction,
    bool CompanyIntroductionEnabled = false,
    [StringLength(2000)] string CompanyIntroductionMessage = "") : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (CandidateIntroductionEnabled && string.IsNullOrWhiteSpace(CandidateIntroductionMessage))
            yield return new("متن معرفی کارجو الزامی است.", [nameof(CandidateIntroductionMessage)]);
        if (CompanyIntroductionEnabled && string.IsNullOrWhiteSpace(CompanyIntroductionMessage))
            yield return new("متن معرفی شرکت الزامی است.", [nameof(CompanyIntroductionMessage)]);
        if (Questions is null) yield break;
        if (Questions.Select(x => x.Order).Distinct().Count() != Questions.Count ||
            !Questions.Select(x => x.Order).Order().SequenceEqual(Enumerable.Range(1, Questions.Count)))
            yield return new("Question orders must be contiguous and unique.", [nameof(Questions)]);
        var ids = Questions.Where(x => x.Id.HasValue).Select(x => x.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length) yield return new("Duplicate question identifiers.", [nameof(Questions)]);
    }
}
public sealed record UpdateTemplateRequest([Required] TemplateRequest Template,
    [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record QuestionDto(Guid Id, string Text, int Order, string Competency, string EvaluationGuidance);
public sealed record TemplateDto(Guid Id, Guid JobPositionId, string Title, string Language, int DurationMinutes, int MaxFollowUpQuestionsPerQuestion, bool IsArchived, byte[] RowVersion, IReadOnlyList<QuestionDto> Questions,
    string InterviewType = "general", string WelcomeMessage = "", string GoodbyeMessage = "", bool UseTopicQuestions = false,
    IReadOnlyList<HireYar.Application.Interviews.QuestionTopicRequest>? Topics = null, int InterviewQuestionCount = 0,
    bool CandidateIntroductionEnabled = false, string CandidateIntroductionMessage = InterviewTemplate.DefaultCandidateIntroduction,
    bool CompanyIntroductionEnabled = false, string CompanyIntroductionMessage = "");
public sealed record TemplateListDto(Guid Id, Guid JobPositionId, string Title, string Language, int DurationMinutes, bool IsArchived, byte[] RowVersion,
    string InterviewType = "general", int QuestionCount = 0, bool UseTopicQuestions = false, int InterviewQuestionCount = 0);
