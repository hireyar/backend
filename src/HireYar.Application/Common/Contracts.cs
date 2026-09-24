using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Common;

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid OrganizationId { get; }
    OrganizationRole Role { get; }
}
public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<JobPosition> JobPositions { get; }
    DbSet<InterviewTemplate> InterviewTemplates { get; }
    DbSet<InterviewQuestion> InterviewQuestions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    void SetOriginalVersion(object entity, byte[] version);
    Task ReplaceQuestionsAsync(InterviewTemplate template, IReadOnlyList<InterviewQuestion> questions, CancellationToken ct);
}
public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken ct);
}
public sealed class AppException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public static AppException NotFound() => new(404, "not_found", "Record not found.");
    public static AppException Invalid(string code = "invalid_request") => new(400, code, "The request is invalid.");
    public static AppException Unauthorized() => new(401, "invalid_credentials", "Authentication failed.");
    public static AppException Conflict() => new(409, "conflict", "The record changed or already exists.");
}
public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);
public sealed class PageQuery
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [System.ComponentModel.DataAnnotations.Range(1, 100)] public int PageSize { get; set; } = 20;
    public int Skip => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);
}
