using Microsoft.EntityFrameworkCore;
using FinanceBot.Data;
using FinanceBot.Models;
using System.Globalization;
using FinanceBot.Services;
using Microsoft.Extensions.Options;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.BotToken), "Telegram:BotToken deve ser configurado.")
    .ValidateOnStart();

builder.Services.AddSingleton<ITelegramBotClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    return new TelegramBotClient(options.BotToken);
});

builder.Services.AddScoped<IFinanceMessageProcessor, FinanceService>();
builder.Services.AddHostedService<TelegramBotService>();

var app = builder.Build();

app.MapGet("/", () => "API FinanceBot Rodando!");

app.Run();
