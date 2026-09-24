using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.JobPositions;

public sealed record JobRequest(
    [Required, StringLength(200)] string Title,
    [Required, StringLength(10000)] string Description,
    [Required, StringLength(100)] string Seniority,
    [EnumDataType(typeof(JobStatus))] JobStatus Status);
public sealed record UpdateJobRequest(
    [Required] JobRequest Job,
    [Required, MinLength(8), MaxLength(8)] byte[] RowVersion);
public sealed record JobDto(Guid Id, string Title, string Description, string Seniority, JobStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, byte[] RowVersion);
