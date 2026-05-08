namespace FinanceBot.Application.Contracts;

public interface IAccessPolicyService
{
    Task EnsureCanRegisterLancamentoAsync(CancellationToken cancellationToken = default);
    Task EnsureCanUsePremiumFeatureAsync(string featureName, CancellationToken cancellationToken = default);
    Task<BillingStatusDto> ObterStatusAtualAsync(CancellationToken cancellationToken = default);
    Task<SolicitacaoUpgradeDto> SolicitarUpgradeAsync(CancellationToken cancellationToken = default);
}
