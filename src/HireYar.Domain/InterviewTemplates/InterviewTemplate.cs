namespace HireYar.Domain.InterviewTemplates;

public sealed class InterviewTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid JobPositionId { get; set; }
    public string Title { get; set; } = "";
    public string Language { get; set; } = "fa-IR";
    public string InterviewType { get; set; } = "general";
    public string WelcomeMessage { get; set; } = "به مصاحبه خوش آمدید.";
    public string GoodbyeMessage { get; set; } = "از وقتی که گذاشتید متشکریم.";
    public const string DefaultCandidateIntroduction = "قبل از شروع سؤال‌ها، لطفاً خودتون رو معرفی کنید و درباره سوابق کاری و پروژه‌هایی که روی آن‌ها کار کردین برای ما توضیح بدین";
    public string CandidateIntroductionMessage { get; set; } = DefaultCandidateIntroduction;
    public bool CandidateIntroductionEnabled { get; set; }
    public string CompanyIntroductionMessage { get; set; } = "";
    public bool CompanyIntroductionEnabled { get; set; }
    public int DurationMinutes { get; set; }
    public int MaxFollowUpQuestionsPerQuestion { get; set; }
    public bool UseTopicQuestions { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<InterviewQuestion> Questions { get; set; } = [];
}
public sealed class InterviewQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InterviewTemplateId { get; set; }
    public string Text { get; set; } = "";
    public int Order { get; set; }
    public string? TopicName { get; set; }
    public int? TopicOrder { get; set; }
    public int Difficulty { get; set; } = 3;
    public string Competency { get; set; } = "";
    public string EvaluationGuidance { get; set; } = "";
}
