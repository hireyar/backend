using System.ComponentModel.DataAnnotations;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireYar.Api.Controllers;

// Employer panel only. Candidate invitation credentials cannot access exchange rates or balances.
[ApiController, Route("api/interview-pricing"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Owner,Recruiter")]
public sealed class InterviewPricingController(ICurrentUser user, InvitationPricingService pricing) : ControllerBase
{
    [HttpGet("quote")]
    public Task<InvitationPriceDto> Quote([FromQuery] Guid templateId, [FromQuery] Guid? packageId,
        [FromQuery, RegularExpression("^(general|specialized)$")] string? interviewType, CancellationToken ct)
        => pricing.QuoteAsync(user.OrganizationId, templateId, ct, packageId, interviewType);
}
