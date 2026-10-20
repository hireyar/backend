using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Common;

public class PageQuery
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [System.ComponentModel.DataAnnotations.Range(1, 100)] public int PageSize { get; set; } = 20;
    public int Skip => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);
}
