using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
using HireYar.Application.Interviews;
namespace HireYar.Application.InterviewTemplates;

public sealed class TemplateService(IApplicationDbContext db, ICurrentUser user, TimeProvider time, HireYar.Application.Billing.ISubscriptionEntitlementService entitlements)
{
    private IQueryable<InterviewTemplate> Scoped => db.InterviewTemplates.Where(x => x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null);
    private static TemplateDto Map(InterviewTemplate x) => new(x.Id, x.JobPositionId, x.Title, x.Language, x.DurationMinutes, x.MaxFollowUpQuestionsPerQuestion, x.IsArchived, x.RowVersion,
        x.Questions.OrderBy(q => q.Order).Select(q => new QuestionDto(q.Id, q.Text, q.Order, q.Competency, q.EvaluationGuidance)).ToArray(), x.InterviewType, x.WelcomeMessage, x.GoodbyeMessage, x.UseTopicQuestions,
        x.UseTopicQuestions ? x.Questions.GroupBy(q => q.TopicOrder).OrderBy(g => g.Key).Select(g => new QuestionTopicRequest(g.First().TopicName!,
            g.OrderBy(q => q.Difficulty).Select(q => new TopicQuestionRequest(QuestionTopicRules.Level(q.Difficulty), q.Text)).ToList())).ToArray() : null,
        x.UseTopicQuestions ? x.Questions.Count / 3 * 2 : x.Questions.Count,
        x.CandidateIntroductionEnabled, x.CandidateIntroductionMessage, x.CompanyIntroductionEnabled, x.CompanyIntroductionMessage);
    public async Task<Page<TemplateListDto>> ListAsync(PageQuery page, Guid? jobPositionId, CancellationToken ct)
    {
        var query = Scoped.AsNoTracking(); if (jobPositionId.HasValue) query = query.Where(x => x.JobPositionId == jobPositionId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x => new TemplateListDto(x.Id, x.JobPositionId, x.Title, x.Language, x.DurationMinutes, x.IsArchived, x.RowVersion, x.InterviewType, x.Questions.Count, x.UseTopicQuestions, x.UseTopicQuestions ? x.Questions.Count / 3 * 2 : x.Questions.Count)).ToListAsync(ct);
        return new(rows, total, page.Page, page.PageSize);
    }
    public async Task<TemplateDto> GetAsync(Guid id, CancellationToken ct) => Map(await Scoped.AsNoTracking().Include(x => x.Questions).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound());
    public Task<TemplateDto> CreateAsync(TemplateRequest request, CancellationToken ct) => db.WithOrganizationBillingLockAsync(user.OrganizationId, () => CreateAsyncLocked(request, ct), ct);
    private async Task<TemplateDto> CreateAsyncLocked(TemplateRequest request, CancellationToken ct)
    {
        await CheckJob(request.JobPositionId, ct);
        if (request.Questions.Any(x => x.Id.HasValue)) throw AppException.Invalid("unknown_question");
        var row = new InterviewTemplate { OrganizationId = user.OrganizationId, CreatedAtUtc = time.GetUtcNow() };
        await CheckTitle(request.Title, row.Id, row.Title, ct);
        Apply(row, request); row.Questions = Questions(row.Id, request); db.InterviewTemplates.Add(row); await db.SaveChangesAsync(ct); return Map(row);
    }
    public Task<TemplateDto> SaveWizardAsync(Guid? id, WizardTemplateRequest r, byte[]? version, CancellationToken ct) => db.WithOrganizationBillingLockAsync(user.OrganizationId, () => SaveWizardAsyncLocked(id, r, version, ct), ct);
    private async Task<TemplateDto> SaveWizardAsyncLocked(Guid? id, WizardTemplateRequest r, byte[]? version, CancellationToken ct)
    {
        await CheckJob(r.JobPositionId, ct);
        var row = id.HasValue ? await Scoped.Include(x => x.Questions).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound() : new InterviewTemplate { OrganizationId = user.OrganizationId, CreatedAtUtc = time.GetUtcNow() };
        await CheckTitle(r.Title, row.Id, row.Title, ct);
        // Existing session FKs require templates to retain their position. Cross-position reuse is a copy at invitation.
        if (id.HasValue && row.JobPositionId != r.JobPositionId) throw AppException.Invalid("template_position_immutable");
        row.InterviewType = r.InterviewType; row.WelcomeMessage = r.WelcomeMessage.Trim(); row.GoodbyeMessage = r.GoodbyeMessage.Trim();
        if (r.Topics is not null)
        {
            if (!QuestionTopicRules.IsValid(r.Topics) || r.Questions is not null) throw AppException.Invalid("invalid_question_topics");
            await entitlements.RequireAsync(user.OrganizationId, ["advanced_ai_workflow", "adaptive_questions", "adaptive_difficulty"], ct);
        }
        row.UseTopicQuestions = r.Topics is not null;
        var request = new TemplateRequest(r.JobPositionId, r.Title, r.DurationMinutes, 0,
            (r.Questions ?? r.Topics!.SelectMany(t => t.Questions.OrderBy(q => QuestionTopicRules.Difficulty(q.Level)).Select(q => q.Text)).ToList()).Select((text, index) => new QuestionRequest(null, text, index + 1, "ارزیابی شغلی", "شواهد مرتبط با شرح شغل را بررسی کنید.")).ToList());
        Apply(row, request);
        row.CandidateIntroductionEnabled = r.CandidateIntroductionEnabled; row.CandidateIntroductionMessage = r.CandidateIntroductionMessage?.Trim() ?? "";
        row.CompanyIntroductionEnabled = r.CompanyIntroductionEnabled; row.CompanyIntroductionMessage = r.CompanyIntroductionMessage?.Trim() ?? "";
        var questions = Questions(row.Id, request);
        if (r.Topics is not null)
            for (var i = 0; i < r.Topics.Count; i++)
                foreach (var q in questions.Skip(i * 3).Take(3))
                { q.TopicOrder = i + 1; q.TopicName = r.Topics[i].Name.Trim(); q.Competency = q.TopicName; q.Difficulty = new[] { 1, 3, 5 }[(q.Order - 1) % 3]; }
        if (id.HasValue) { db.SetOriginalVersion(row, version!); await db.ReplaceQuestionsAsync(row, questions, ct); }
        else { row.Questions = questions; db.InterviewTemplates.Add(row); await db.SaveChangesAsync(ct); }
        return Map(row);
    }
    public Task<TemplateDto> UpdateAsync(Guid id, UpdateTemplateRequest request, CancellationToken ct) => db.WithOrganizationBillingLockAsync(user.OrganizationId, () => UpdateAsyncLocked(id, request, ct), ct);
    private async Task<TemplateDto> UpdateAsyncLocked(Guid id, UpdateTemplateRequest request, CancellationToken ct)
    {
        var row = await Scoped.Include(x => x.Questions).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        if (row.UseTopicQuestions) throw AppException.Invalid("use_wizard_for_topic_template");
        await CheckJob(request.Template.JobPositionId, ct);
        if (request.Template.Questions.Any(x => x.Id.HasValue && row.Questions.All(q => q.Id != x.Id))) throw AppException.Invalid("unknown_question");
        await CheckTitle(request.Template.Title, row.Id, row.Title, ct);
        db.SetOriginalVersion(row, request.RowVersion); Apply(row, request.Template);
        await db.ReplaceQuestionsAsync(row, Questions(row.Id, request.Template), ct); return Map(row);
    }
    private async Task CheckTitle(string title, Guid id, string currentTitle, CancellationToken ct)
    {
        var normalized = TitleComparison.Normalize(title);
        if (normalized.Length == 0) throw new AppException(400, "invalid_title", "عنوان باید دارای متن باشد.");
        // Keeping a historical title must remain possible after an internal invitation copy.
        if (normalized == TitleComparison.Normalize(currentTitle)) return;
        if (await TitleComparison.ExistsAsync(Scoped.Where(x => x.Id != id).Select(x => x.Title), normalized, ct))
            throw new AppException(409, "template_title_duplicate", "عنوان قالب مصاحبه قبلاً ثبت شده است.");
    }
    private async Task CheckJob(Guid id, CancellationToken ct)
    { if (!await db.JobPositions.AnyAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null, ct)) throw AppException.NotFound(); }
    public Task DeleteAsync(Guid id, CancellationToken ct) => db.WithOrganizationBillingLockAsync(user.OrganizationId, async () =>
    {
        var row = await Scoped.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        row.DeletedAtUtc = time.GetUtcNow(); row.UpdatedAtUtc = row.DeletedAtUtc.Value; row.IsArchived = true;
        return true;
    }, ct);
    private static List<InterviewQuestion> Questions(Guid id, TemplateRequest r) => r.Questions.Select(q => new InterviewQuestion
    {
        Id = q.Id ?? Guid.NewGuid(),
        InterviewTemplateId = id,
        Text = q.Text.Trim(),
        Order = q.Order,
        Competency = q.Competency.Trim(),
        EvaluationGuidance = q.EvaluationGuidance.Trim()
    }).ToList();
    private void Apply(InterviewTemplate row, TemplateRequest r)
    {
        row.JobPositionId = r.JobPositionId; row.Title = r.Title.Trim(); row.Language = r.Language.Trim(); row.DurationMinutes = r.DurationMinutes;
        row.MaxFollowUpQuestionsPerQuestion = r.MaxFollowUpQuestionsPerQuestion; row.IsArchived = r.IsArchived; row.UpdatedAtUtc = time.GetUtcNow();
        row.CandidateIntroductionEnabled = r.CandidateIntroductionEnabled; row.CandidateIntroductionMessage = r.CandidateIntroductionMessage?.Trim() ?? "";
        row.CompanyIntroductionEnabled = r.CompanyIntroductionEnabled; row.CompanyIntroductionMessage = r.CompanyIntroductionMessage?.Trim() ?? "";
    }
}
