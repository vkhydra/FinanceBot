using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FinanceBot.Api.Tests.Infrastructure;

public sealed class FinanceBotWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public FinanceBotWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Jwt:Issuer"] = "FinanceBot",
                ["Jwt:Audience"] = "FinanceBot.Client",
                ["Jwt:Key"] = "super-secret-test-key-with-at-least-32-characters",
                ["Jwt:TokenExpirationMinutes"] = "60",
                ["Billing:FreeMonthlyLaunchLimit"] = "2",
                ["Billing:DefaultTrialDays"] = "7"
            });
        });
    }
}
