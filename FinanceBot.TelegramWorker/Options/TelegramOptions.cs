using System.ComponentModel.DataAnnotations;

namespace FinanceBot.TelegramWorker.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    [Required]
    public string BotToken { get; init; } = string.Empty;
}
