using System.Security.Claims;
using HireYar.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HireYar.Api.Notifications;

[Authorize(Roles = "Owner")]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
    internal static string Group(Guid organizationId, Guid userId) => $"notifications:{organizationId:N}:{userId:N}";
    public override async Task OnConnectedAsync()
    {
        var org = Guid.Parse(Context.User!.FindFirstValue("org")!);
        var user = Guid.Parse(Context.User!.FindFirstValue("sub")!);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(org, user), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}

public sealed class SignalRNotificationPublisher(IHubContext<NotificationsHub> hub) : INotificationPublisher
{
    public Task ChangedAsync(Guid organizationId, Guid recipientUserId, CancellationToken ct) =>
        hub.Clients.Group(NotificationsHub.Group(organizationId, recipientUserId)).SendAsync("NotificationsChanged", ct);
}
