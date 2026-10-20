using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;

namespace HireYar.Application.Subscriptions;

public sealed class InterviewUsageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [RegularExpression("^(Created|InProgress|Completed|Cancelled|Expired|Replaced|Revoked|Deleted)$")]
    public string? Status { get; set; }
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
}

public sealed record InterviewUsageDto(Guid InterviewId, string CandidateName, string? PositionTitle,
    string? TemplateTitle, string InterviewType, string Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc, int ConfiguredDurationSeconds, DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc, int? ActualDurationSeconds, int CreditsConsumed, bool IsReserved,
    Guid? ReplacementInterviewId);
public sealed record InterviewUsageEventDto(Guid Id, string EventType, DateTimeOffset OccurredAtUtc,
    int CreditsConsumed, bool IsHistorical);
public sealed record InterviewUsageDetailDto(InterviewUsageDto Interview, IReadOnlyList<InterviewUsageEventDto> Events);
