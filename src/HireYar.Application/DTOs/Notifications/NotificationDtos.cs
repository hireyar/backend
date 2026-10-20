namespace HireYar.Application.Notifications;

public sealed record NotificationDto(Guid Id, string Type, string Title, string Target, Guid? InterviewId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc);
public sealed record NotificationListDto(IReadOnlyList<NotificationDto> Items, int Total, int UnreadCount, int Page, int PageSize);
public sealed record NotificationCountDto(int UnreadCount);
public sealed record NotificationReadRequest(bool IsRead);
