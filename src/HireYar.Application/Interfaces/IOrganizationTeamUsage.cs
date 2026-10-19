namespace HireYar.Application.Subscriptions;
public interface IOrganizationTeamUsage
{
    Task<int> OccupiedAsync(Guid organizationId, CancellationToken ct);
}
