using HireYar.Application.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireYar.Api.Controllers;

[Route("api/admin/tickets"), Authorize(Policy = "SystemAdmin")]
public sealed class AdminTicketsController(TicketService tickets) : TicketsControllerBase(tickets)
{
    protected override bool Administrator => true;
}

