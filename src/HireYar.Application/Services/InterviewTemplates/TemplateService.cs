using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
using HireYar.Application.Interviews;
namespace HireYar.Application.InterviewTemplates;

public sealed class TemplateService(IApplicationDbContext db, ICurrentUser user, TimeProvider time)
{
    private IQueryable<InterviewTemplate> Scoped => db.InterviewTemplates.Where(x => x.OrganizationId == user.OrganizationId);
    private static TemplateDto Map(InterviewTemplate x) => new(x.Id, x.JobPositionId, x.Title, x.Language, x.DurationMinutes, x.MaxFollowUpQuestionsPerQuestion, x.IsArchived, x.RowVersion,
        x.Questions.OrderBy(q => q.Order).Select(q => new QuestionDto(q.Id, q.Text, q.Order, q.Competency, q.EvaluationGuidance)).ToArray(), x.InterviewType, x.WelcomeMessage, x.GoodbyeMessage);
    public async Task<Page<TemplateListDto>> ListAsync(PageQuery page, Guid? jobPositionId, CancellationToken ct)
    {
        var query = Scoped.AsNoTracking(); if (jobPositionId.HasValue) query = query.Where(x => x.JobPositionId == jobPositionId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x => new TemplateListDto(x.Id, x.JobPositionId, x.Title, x.Language, x.DurationMinutes, x.IsArchived, x.RowVersion, x.InterviewType, x.Questions.Count)).ToListAsync(ct);
        return new(rows, total, page.Page, page.PageSize);
    }
    public async Task<TemplateDto> GetAsync(Guid id, CancellationToken ct) => Map(await Scoped.AsNoTracking().Include(x => x.Questions).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound());
    public async Task<TemplateDto> CreateAsync(TemplateRequest request, CancellationToken ct)
    {
        await CheckJob(request.JobPositionId, ct);
        if (request.Questions.Any(x => x.Id.HasValue)) throw AppException.Invalid("unknown_question");
        var row = new InterviewTemplate { OrganizationId = user.OrganizationId, CreatedAtUtc = time.GetUtcNow() };
        Apply(row, request); row.Questions = Questions(row.Id, request); db.InterviewTemplates.Add(row); await db.SaveChangesAsync(ct); return Map(row);
    }
    public async Task<TemplateDto> SaveWizardAsync(Guid? id, WizardTemplateRequest r, byte[]? version, CancellationToken ct)
    {
        await CheckJob(r.JobPositionId, ct);
        var row = id.HasValue ? await Scoped.Include(x => x.Questions).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound() : new InterviewTemplate { OrganizationId = user.OrganizationId, CreatedAtUtc = time.GetUtcNow() };
        // Existing session FKs require templates to retain their position. Cross-position reuse is a copy at invitation.
        if (id.HasValue && row.JobPositionId != r.JobPositionId) throw AppException.Invalid("template_position_immutable");
        row.InterviewType = r.InterviewType; row.WelcomeMessage = r.WelcomeMessage.Trim(); row.GoodbyeMessage = r.GoodbyeMessage.Trim();
        var request = new TemplateRequest(r.JobPositionId, r.Title, r.DurationMinutes, 0,
            r.Questions.Select((text, index) => new QuestionRequest(null, text, index + 1, "ارزیابی شغلی", "شواهد مرتبط با شرح شغل را بررسی کنید.")).ToList());
        Apply(row, request);
        if (id.HasValue) { db.SetOriginalVersion(row, version!); await db.ReplaceQuestionsAsync(row, Questions(row.Id, request), ct); }
        else { row.Questions = Questions(row.Id, request); db.InterviewTemplates.Add(row); await db.SaveChangesAsync(ct); }
        return Map(row);
    }
    public async Task<TemplateDto> UpdateAsync(Guid id, UpdateTemplateRequest request, CancellationToken ct)
    {
        var row = await Scoped.Include(x => x.Questions).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        await CheckJob(request.Template.JobPositionId, ct);
        if (request.Template.Questions.Any(x => x.Id.HasValue && row.Questions.All(q => q.Id != x.Id))) throw AppException.Invalid("unknown_question");
        db.SetOriginalVersion(row, request.RowVersion); Apply(row, request.Template);
        await db.ReplaceQuestionsAsync(row, Questions(row.Id, request.Template), ct); return Map(row);
    }
    private async Task CheckJob(Guid id, CancellationToken ct)
    { if (!await db.JobPositions.AnyAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct)) throw AppException.NotFound(); }
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
    }
}
