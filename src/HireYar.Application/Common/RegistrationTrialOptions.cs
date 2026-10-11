namespace HireYar.Application.Billing;

public sealed class RegistrationTrialOptions
{
    public int DurationDays { get; set; } = 3;
    public decimal CreditAmountUsd { get; set; } = 1m;
}
