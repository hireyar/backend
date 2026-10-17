namespace HireYar.Application.Billing;

public sealed class RegistrationTrialOptions
{
    public int DurationDays { get; set; } = 3;
    public decimal CreditAmountRial { get; set; } = 1_000_000m;
}
