namespace FinanceBot.Application.Contracts;

public interface IAccessPolicyService
{
    Task EnsureCanRegisterLancamentoAsync(CancellationToken cancellationToken = default);
    Task<BillingStatusDto> ObterStatusAtualAsync(CancellationToken cancellationToken = default);
}
