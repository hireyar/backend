using HireYar.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireYar.Api.Controllers;

[ApiController, Route("api/notifications"), Authorize(Roles = "Owner,Recruiter")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet]
    public Task<NotificationListDto> List(CancellationToken ct, [FromQuery] string? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => service.ListAsync(status, page, pageSize, ct);

    [HttpGet("unread-count")]
    public Task<NotificationCountDto> Count(CancellationToken ct) => service.CountAsync(ct);

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, NotificationReadRequest request, CancellationToken ct)
    {
        await service.SetReadAsync(id, request.IsRead, ct);
        return NoContent();
    }
}
