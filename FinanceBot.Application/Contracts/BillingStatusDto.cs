namespace FinanceBot.Application.Contracts;

public sealed record BillingStatusDto(
    Guid UsuarioId,
    string PlanoAtual,
    string PlanoEfetivo,
    string StatusAssinatura,
    DateTime? PremiumAteUtc,
    DateTime? TrialAteUtc,
    DateTime? UpgradeSolicitadoEmUtc,
    int LancamentosNoMesAtual,
    int? LimiteLancamentosNoMesAtual,
    bool PodeRegistrarLancamento,
    bool UpgradePendente,
    bool PodeSolicitarUpgrade,
    string? MotivoBloqueio,
    bool TrialAtivo,
    int? DiasRestantesTrial,
    string MensagemStatus,
    string? MensagemUpgrade);
