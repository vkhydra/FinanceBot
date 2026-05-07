using System.ComponentModel.DataAnnotations;

namespace FinanceBot.Services;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    [Required]
    public string BotToken { get; init; } = string.Empty;
}
