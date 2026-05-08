namespace FinanceBot.Application.Contracts;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public int FreeMonthlyLaunchLimit { get; init; } = 50;
    public int DefaultTrialDays { get; init; } = 7;
}
