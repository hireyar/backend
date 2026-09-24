using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Organizations;

public sealed record OrganizationDto(Guid Id, string Name);
public sealed record OrganizationRequest([Required, StringLength(200)] string Name);
