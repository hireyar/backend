using HireYar.Application.Common;
using HireYar.Application.Members;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Dashboard;

public sealed record JobStatusCount(JobStatus Status, int Count);
public sealed record DashboardDto(IReadOnlyList<JobStatusCount> Jobs, int Templates, int ActiveMembers, int PendingInvitations);
