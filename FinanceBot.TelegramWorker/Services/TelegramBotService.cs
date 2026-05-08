using System.Data.Common;
using FinanceBot.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FinanceBot.TelegramWorker.Services;

public sealed class TelegramBotService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(
        ITelegramBotClient botClient,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TelegramBotService> logger)
    {
        _botClient = botClient;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var botInfo = await _botClient.GetMe(cancellationToken: stoppingToken);

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandlePollingErrorAsync,
            new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message }
            },
            stoppingToken);

        _logger.LogInformation("Bot do Telegram @{Username} iniciado.", botInfo.Username);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message?.Text is not { Length: > 0 } corpoMensagem)
        {
            return;
        }

        FinanceMessageResult resultado;

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var currentUserContext = scope.ServiceProvider.GetRequiredService<ICurrentUserContextAccessor>();
            currentUserContext.SetTelegramChatId(update.Message.Chat.Id);

            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            var usuario = await identityService.ObterUsuarioPorTelegramAsync(update.Message.Chat.Id, cancellationToken);
            if (usuario is not null)
            {
                currentUserContext.SetAuthenticatedUser(usuario.UsuarioId, usuario.Email);
            }

            var financeMessageProcessor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();

            resultado = await financeMessageProcessor.ProcessarMensagemAsync(
                new FinanceMessageRequest(corpoMensagem, update.Message.Chat.Id),
                cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Falha ao acessar o banco durante o processamento da mensagem do Telegram.");
            resultado = FinanceMessageResult.FalhaHtml("⚠️ <b>Instabilidade temporária</b>\nNão consegui acessar os dados agora. Tente novamente em instantes.");
        }
        catch (DbException ex)
        {
            _logger.LogError(ex, "Falha ao acessar o banco durante o processamento da mensagem do Telegram.");
            resultado = FinanceMessageResult.FalhaHtml("⚠️ <b>Instabilidade temporária</b>\nNão consegui acessar os dados agora. Tente novamente em instantes.");
        }
        catch (InvalidOperationException ex) when (HasDatabaseFailure(ex))
        {
            _logger.LogError(ex, "Falha transiente de banco durante o processamento da mensagem do Telegram.");
            resultado = FinanceMessageResult.FalhaHtml("⚠️ <b>Instabilidade temporária</b>\nNão consegui acessar os dados agora. Tente novamente em instantes.");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout ao acessar o banco durante o processamento da mensagem do Telegram.");
            resultado = FinanceMessageResult.FalhaHtml("⚠️ <b>Instabilidade temporária</b>\nNão consegui acessar os dados agora. Tente novamente em instantes.");
        }

        if (resultado.UsaHtml)
        {
            await botClient.SendMessage(
                chatId: update.Message.Chat.Id,
                text: resultado.Mensagem,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            return;
        }

        await botClient.SendMessage(
            chatId: update.Message.Chat.Id,
            text: resultado.Mensagem,
            cancellationToken: cancellationToken);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Erro ao processar atualizacao do Telegram.");
        return Task.CompletedTask;
    }

    private static bool HasDatabaseFailure(Exception exception)
    {
        for (Exception? atual = exception; atual is not null; atual = atual.InnerException)
        {
            if (atual is DbException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
