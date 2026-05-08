namespace FinanceBot.Application.Contracts;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "FinanceBot";
    public string Audience { get; init; } = "FinanceBot.Client";
    public string Key { get; init; } = string.Empty;
    public int TokenExpirationMinutes { get; init; } = 60;
}
