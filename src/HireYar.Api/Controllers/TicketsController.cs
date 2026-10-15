using HireYar.Application.Support;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireYar.Api.Controllers;

[Route("api/tickets"), Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Owner,Recruiter")]
public sealed class TicketsController(TicketService tickets) : TicketsControllerBase(tickets)
{
    protected override bool Administrator => false;
    [HttpPost, Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024, ValueLengthLimit = 10000, ValueCountLimit = 10, MultipartHeadersLengthLimit = 4096)]
    [ProducesResponseType<TicketDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketDto>> Create([FromForm] TicketCreateForm form, CancellationToken ct)
    {
        var ticket = await Tickets.CreateAsync(form.Subject, form.Body, form.Priority, await ReadFilesAsync(form.Files, ct), ct);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, ticket);
    }
}

