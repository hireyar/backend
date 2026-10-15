using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Support;

namespace HireYar.Application.Support;

public sealed record TicketDto(Guid Id, Guid OrganizationId, string OrganizationName, Guid CreatedByUserId,
    string Subject, TicketStatus Status, TicketPriority Priority, long Revision, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record TicketAttachmentDto(Guid Id, string FileName, string ContentType, int Length);
public sealed record TicketMessageDto(Guid Id, Guid AuthorUserId, bool IsStaff, string Body, DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TicketAttachmentDto> Attachments);
public sealed record TicketPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public class TicketPageQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 50)] public int PageSize { get; set; } = 20;
}
public sealed class TicketQuery : TicketPageQuery
{
    [EnumDataType(typeof(TicketStatus))] public TicketStatus? Status { get; set; }
    [EnumDataType(typeof(TicketPriority))] public TicketPriority? Priority { get; set; }
}
public sealed record TicketStatusRequest([EnumDataType(typeof(TicketStatus))] TicketStatus Status, [Range(typeof(long), "1", "9223372036854775807")] long Revision);
public sealed record TicketUpload(string FileName, string ContentType, byte[] Content);
public sealed record TicketFile(string FileName, Stream Content);

