namespace HireYar.Domain.Billing;

public sealed class InterviewTariff
{
    public string InterviewType { get; set; } = "";
    public decimal RatePerMinuteUsd { get; set; }
}
