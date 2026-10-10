using System.Text.Json;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Interviews;

public sealed class QuestionSuggestionService(IApplicationDbContext db, ICurrentUser user,
    ISubscriptionEntitlementService entitlements, IQuestionSuggestionProvider provider)
{
    public Task<QuestionSuggestionsDto> GenerateAsync(QuestionSuggestionRequest r, CancellationToken ct) => Generate(r.JobPositionId, r.TopicCount, null, ct);
    public Task<QuestionSuggestionsDto> RegenerateTopicAsync(TopicSuggestionRequest r, CancellationToken ct) => Generate(r.JobPositionId, 1, r.TopicName.Trim(), ct);
    private async Task<QuestionSuggestionsDto> Generate(Guid jobId, int? count, string? name, CancellationToken ct)
    {
        var job = await db.JobPositions.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId && j.OrganizationId == user.OrganizationId && j.DeletedAtUtc == null, ct)
            ?? throw AppException.NotFound();
        if (job.Status == HireYar.Domain.JobPositions.JobStatus.Archived) throw AppException.Invalid("position_archived");
        await entitlements.RequireAsync(user.OrganizationId, ["advanced_ai_workflow", "adaptive_questions", "adaptive_difficulty"], ct);
        if (string.IsNullOrWhiteSpace(job.Description)) throw AppException.Invalid("job_description_required");
        string json;
        try { json = await provider.SuggestAsync(new(job.Title, job.Description, count, name), ct); }
        catch (InterviewProviderException) { throw new AppException(503, "question_suggestions_unavailable", "تولید پیشنهاد سؤال در دسترس نیست؛ دوباره تلاش کنید."); }
        List<QuestionTopicRequest> topics;
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 128000) throw new JsonException();
            using var doc = JsonDocument.Parse(json);
            topics = doc.RootElement.GetProperty("topics").Deserialize<List<QuestionTopicRequest>>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new JsonException();
            if (!QuestionTopicRules.IsValid(topics, name is not null) || count.HasValue && topics.Count != count ||
                name is not null && !string.Equals(topics[0].Name.Trim(), name, StringComparison.Ordinal)) throw new JsonException();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or NotSupportedException)
        { throw new AppException(502, "question_suggestions_invalid", "ساختار پیشنهاد AI معتبر نیست؛ دوباره تولید کنید."); }
        // Suggestions are transient. Only the explicit wizard save persists the user-approved bank.
        return new(jobId, topics, topics.Count, topics.Count * 3, topics.Count * 3);
    }
}
