namespace FinanceBot.Application.Contracts;

public sealed record BillingStatusDto(
    Guid UsuarioId,
    string PlanoAtual,
    string PlanoEfetivo,
    string StatusAssinatura,
    DateTime? PremiumAteUtc,
    DateTime? TrialAteUtc,
    int LancamentosNoMesAtual,
    int? LimiteLancamentosNoMesAtual,
    bool PodeRegistrarLancamento,
    string? MotivoBloqueio);
