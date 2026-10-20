using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Application.Subscriptions;
namespace HireYar.Application.Administration;

public sealed class AdminCustomerQuery : PageQuery
{
    [StringLength(256)] public string? Search { get; set; }
    [RegularExpression("^(Won|Registered|Waiting|Expired|Stopped|Expiring)$")] public string? Status { get; set; }
    public Guid? AgentId { get; set; }
    public bool? HasSeller { get; set; }
}
public sealed record AdminCustomerDto(Guid Id, string Kind, Guid? OrganizationId, Guid? LeadId, string FullName,
    string OrganizationName, string? Mobile, string? Email, bool IsActive, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RegisteredAtUtc, string RegistrationStatus, Guid? AgentId, string? AgentName,
    string SubscriptionStatus, DateTimeOffset? SubscriptionStartedAtUtc, DateTimeOffset? SubscriptionExpiresAtUtc,
    int RemainingDays, long RemainingInterviews, long AvailableInterviews, int ReservedInterviews,
    int ConsumedInterviews, string? LatestBundleName, string ProfileVersion);
public sealed record AdminCustomerStats(int Total, int Won, int Registered, int Waiting, int Expired, int WithoutSeller, int ExpiringSoon);
public sealed record AdminCustomerPage(Page<AdminCustomerDto> Rows, AdminCustomerStats Stats);
public sealed record AdminCustomerDetail(AdminCustomerDto Customer, SubscriptionStateDto? Subscription,
    string? SubscriptionVersion, bool CanManageSubscription, bool CanEditIdentity);
public sealed record AdminCustomerProfileRequest(Guid RequestId, [Required] string ExpectedVersion,
    [Required, StringLength(200)] string FullName, [StringLength(200)] string? OrganizationName,
    [Required, RegularExpression("^09[0-9]{9}$")] string Mobile,
    [EmailAddress, StringLength(256)] string? Email, [Required, StringLength(1000)] string Reason);
public sealed record AdminCustomerSellerRequest(Guid RequestId, [Required] string ExpectedVersion,
    Guid? AgentId, [Required, StringLength(1000)] string Reason);
public sealed record AdminCustomerCreditRequest(Guid RequestId, [Range(1,100000)] int Quantity,
    [Required, StringLength(64)] string BundleCode, [Required, StringLength(1000)] string Reason);
public sealed record AdminSubscriptionSetRequest(Guid RequestId, [Required] string ExpectedVersion,
    DateTimeOffset StartedAtUtc, [Range(1,12)] int Months, [Range(-3650,3650)] int DaysDelta,
    [Required, StringLength(1000)] string Reason);
public sealed record AdminSubscriptionDaysRequest(Guid RequestId, [Required] string ExpectedVersion,
    [Range(-3650,3650)] int Days, [Required, StringLength(1000)] string Reason);
public sealed record AdminSubscriptionStopRequest(Guid RequestId, [Required] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);
public sealed record AdminCustomerHistoryDto(Guid Id, string Source, string Action, DateTimeOffset OccurredAtUtc,
    Guid? ActorId, string Description, long? AmountRial, int? InterviewQuantity, string? DetailsJson);
public sealed record AdminCustomerMutationDto(Guid EventId, bool Replayed);
