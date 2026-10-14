namespace HireYar.Application.Billing;

public interface ITeamSeatUsage
{
    // Identity and invitation storage stay in Infrastructure. Caller owns the organization lock.
    Task<int> OccupiedAsync(Guid organizationId, CancellationToken ct);
}
