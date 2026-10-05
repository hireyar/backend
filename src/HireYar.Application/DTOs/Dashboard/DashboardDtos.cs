using HireYar.Application.Common;
using HireYar.Application.Members;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Dashboard;

public sealed record JobStatusCount(JobStatus Status, int Count);
public sealed record DashboardDto(IReadOnlyList<JobStatusCount> Jobs, int Templates, int ActiveMembers, int PendingInvitations,
    int CompletedInterviews = 0, int PendingInterviews = 0, IReadOnlyList<HireYar.Application.Interviews.PanelInterviewDto>? RecentInterviews = null);
