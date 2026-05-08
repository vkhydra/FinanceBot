using FinanceBot.Application;
using FinanceBot.Application.Contracts;
using FinanceBot.Infrastructure;
using FinanceBot.TelegramWorker.Options;
using FinanceBot.TelegramWorker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

ConfigureSharedSettings(builder.Configuration, builder.Environment.ContentRootPath, builder.Environment.EnvironmentName);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection deve ser configurada.");

builder.Services.AddApplication();
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection(BillingOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddInfrastructure(connectionString);

builder.Services
    .AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.BotToken), "Telegram:BotToken deve ser configurado.")
    .ValidateOnStart();

builder.Services.AddSingleton<ITelegramBotClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    return new TelegramBotClient(options.BotToken);
});

builder.Services.AddHostedService<TelegramBotService>();

var host = builder.Build();
host.Run();

static void ConfigureSharedSettings(IConfigurationBuilder configuration, string contentRootPath, string environmentName)
{
    var solutionRoot = TryFindSolutionRoot(contentRootPath);
    if (solutionRoot is null)
    {
        return;
    }

    configuration
        .AddJsonFile(Path.Combine(solutionRoot, "appsettings.json"), optional: true, reloadOnChange: false)
        .AddJsonFile(Path.Combine(solutionRoot, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: false)
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables();
}

static string? TryFindSolutionRoot(string startPath)
{
    var directory = new DirectoryInfo(startPath);

    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "FinanceBot.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return null;
}
