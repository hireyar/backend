using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
namespace HireYar.Application.Sales;

public sealed record SalesLoginRequest([Required, RegularExpression("^09[0-9]{9}$")] string Mobile,
    [Required, StringLength(128, MinimumLength = 12)] string Password);
public sealed record SalesSessionDto(string AccessToken, DateTimeOffset ExpiresAtUtc);
public sealed record SalesAgentRequest([Required, StringLength(200)] string FullName,
    [Required, RegularExpression("^09[0-9]{9}$")] string Mobile,
    [Range(0,100)] decimal FirstSalePercent = 20, [Range(0,100)] decimal RenewalPercent = 5, bool IsActive = true);
public sealed record SalesAgentDto(Guid Id, string FullName, string Mobile, bool IsActive, decimal FirstSalePercent, decimal RenewalPercent);
public sealed record SalesCredentialsDto(SalesAgentDto Agent, string Password);
public sealed record SalesLeadRequest([Required, RegularExpression("^09[0-9]{9}$")] string Mobile,
    [Required, StringLength(200)] string CustomerName, [StringLength(2000)] string Note = "");
public sealed record SalesFollowUpRequest([StringLength(2000)] string Note, DateTimeOffset? FollowUpAtUtc);
public sealed record SalesTransferRequest(Guid TargetAgentId, bool IncludeCustomers, [Required, StringLength(1000)] string Reason);
public sealed record SalesLeadDto(Guid Id, Guid? AgentId, string CustomerName, string Mobile, string Note, string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset LastContactAtUtc, DateTimeOffset? FollowUpAtUtc, DateTimeOffset? RegisteredAtUtc);
public sealed class SalesLeadFilter
{
    public Guid? AgentId { get; set; }
    [RegularExpression("^(Waiting|Registered|Won|Dead)$")] public string? Status { get; set; }
}
public sealed class SalesCommissionFilter
{
    public Guid? AgentId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? PayoutId { get; set; }
    public DateTimeOffset? RegisteredFromUtc { get; set; }
    public DateTimeOffset? RegisteredToUtc { get; set; }
    [RegularExpression("^(Holding|Available|Requested|Paid|Returned)$")] public string? Status { get; set; }
}
public sealed record SalesCommissionDto(Guid Id, Guid AgentId, Guid LeadId, Guid OrderId, string CustomerName,
    DateTimeOffset RegisteredAtUtc, string Kind, long InvoiceAmountRial, decimal Percent, long AmountRial,
    DateTimeOffset CreatedAtUtc, DateTimeOffset AvailableAtUtc, string Status, Guid? PayoutId, bool CanRequest);
public sealed record SalesCommissionPage(Page<SalesCommissionDto> Rows, string Currency, long TotalAmountRial, long AvailableAmountRial, long RequestedAmountRial, long PaidAmountRial);
public sealed record SalesPayoutRequest(Guid RequestId, [Required, MinLength(1), MaxLength(100)] Guid[] CommissionIds);
public sealed record SalesPayoutReviewRequest(bool Paid, [Required, StringLength(1000)] string Note);
public sealed record SalesPayoutDto(Guid Id, Guid AgentId, string AgentName, long AmountRial, string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? ReviewedAtUtc, string? ReviewNote);
public sealed record SalesAuditDto(Guid Id, Guid ActorId, Guid? LeadId, string Action, string Reason, DateTimeOffset CreatedAtUtc);
public sealed record SalesStatsDto(int CallsLast30Days, int Waiting, int Registered, int Won, int Dead, long CurrentMonthCommissionRial, int ActiveAgents, int PendingPayouts);
