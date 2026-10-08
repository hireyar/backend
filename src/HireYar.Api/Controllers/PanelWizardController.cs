using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Domain.JobPositions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireYar.Api.Controllers;

public sealed class WizardInvitationForm
{
    public Guid JobPositionId { get; set; }
    public Guid TemplateId { get; set; }
    [Required, StringLength(200)] public string FirstName { get; set; } = "";
    [Required, StringLength(200)] public string LastName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, RegularExpression("^09[0-9]{9}$")] public string Phone { get; set; } = "";
    [Range(1, 14)] public int DeadlineDays { get; set; } = 3;
    [Required, RegularExpression("^(general|specialized)$")] public string InterviewType { get; set; } = "general";
    [Required] public IFormFile Resume { get; set; } = null!;
    [StringLength(32000)] public string? ResumeText { get; set; }
    public bool SendEmail { get; set; }
    public bool SendSms { get; set; }
}
public sealed record UpdateWizardPositionRequest([Required] WizardPositionRequest Position,
    [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);

[ApiController, Authorize(Roles = "Owner"), Route("api/panel-wizard")]
public sealed class PanelWizardController(JobService jobs, TemplateService templates, PanelWizardService wizard) : ControllerBase
{
    [HttpPost("positions")]
    public Task<JobDto> CreatePosition(WizardPositionRequest r, CancellationToken ct) =>
        jobs.CreateAsync(new(r.Title, r.Description, "NotSpecified", JobStatus.Active), ct);

    [HttpPut("positions/{id:guid}")]
    public async Task<JobDto> UpdatePosition(Guid id, UpdateWizardPositionRequest r, CancellationToken ct)
    {
        var existing = await jobs.GetAsync(id, ct);
        return await jobs.UpdateAsync(id, new(new(r.Position.Title, r.Position.Description, existing.Seniority, existing.Status), r.RowVersion), ct);
    }

    [HttpPost("templates")]
    public Task<TemplateDto> CreateTemplate(WizardTemplateRequest r, CancellationToken ct) => templates.SaveWizardAsync(null, r, null, ct);

    [HttpPut("templates/{id:guid}")]
    public Task<TemplateDto> UpdateTemplate(Guid id, UpdateWizardTemplateRequest r, CancellationToken ct) => templates.SaveWizardAsync(id, r.Template, r.RowVersion, ct);

    [HttpPost("invitations"), Consumes("multipart/form-data"), RequestSizeLimit(11 * 1024 * 1024)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<PanelInvitationResult> Invite([FromForm] WizardInvitationForm r, CancellationToken ct)
    {
        if (r.Resume.Length is <= 0 or > ResumeUploadValidator.MaxBytes) throw AppException.Invalid("invalid_resume_size");
        await using var stream = r.Resume.OpenReadStream();
        return await wizard.InviteAsync(new(r.JobPositionId, r.TemplateId, r.FirstName, r.LastName, r.Email, r.Phone, r.DeadlineDays, r.InterviewType, r.ResumeText, r.SendEmail, r.SendSms), stream, r.Resume.FileName, ct);
    }
}
