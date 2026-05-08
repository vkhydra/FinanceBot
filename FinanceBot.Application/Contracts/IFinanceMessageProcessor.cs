namespace FinanceBot.Application.Contracts;

public interface IFinanceMessageProcessor
{
    Task<FinanceMessageResult> ProcessarMensagemAsync(FinanceMessageRequest request, CancellationToken cancellationToken = default);
}
