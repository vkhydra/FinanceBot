namespace FinanceBot.Services;

public interface IFinanceMessageProcessor
{
    Task<FinanceMessageResult> ProcessarMensagemAsync(FinanceMessageRequest request);
}
