namespace FinanceBot.Application.Contracts;

public sealed record SolicitacaoUpgradeDto(
    bool Sucesso,
    string Mensagem,
    DateTime? SolicitadoEmUtc,
    bool UpgradePendente);
