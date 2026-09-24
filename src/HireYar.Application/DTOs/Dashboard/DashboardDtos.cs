using HireYar.Domain.Hiring;
using HireYar.Domain.JobPositions;
namespace HireYar.Application.Dashboard;

public sealed record JobStatusCount(JobStatus Status, int Count);
public sealed record SessionStatusCount(SessionStatus Status, int Count);
public sealed record DashboardDto(IReadOnlyList<JobStatusCount> Jobs, int Templates, int ActiveMembers, int PendingInvitations, int Candidates, int ActiveApplications, IReadOnlyList<SessionStatusCount> Sessions, int FailedInterviewInvitations);
