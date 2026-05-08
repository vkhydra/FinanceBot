using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;
using Microsoft.Extensions.Options;

namespace FinanceBot.Application.Services;

public sealed class AccessPolicyService : IAccessPolicyService
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAssinaturaUsuarioRepository _assinaturas;
    private readonly ITransacaoRepository _transacoes;
    private readonly IReceitaRepository _receitas;
    private readonly IOptions<BillingOptions> _billingOptions;

    public AccessPolicyService(
        ICurrentUserContext currentUserContext,
        IAssinaturaUsuarioRepository assinaturas,
        ITransacaoRepository transacoes,
        IReceitaRepository receitas,
        IOptions<BillingOptions> billingOptions)
    {
        _currentUserContext = currentUserContext;
        _assinaturas = assinaturas;
        _transacoes = transacoes;
        _receitas = receitas;
        _billingOptions = billingOptions;
    }

    public async Task EnsureCanRegisterLancamentoAsync(CancellationToken cancellationToken = default)
    {
        var status = await ObterStatusAtualAsync(cancellationToken);
        if (!status.PodeRegistrarLancamento)
        {
            throw new AccessPolicyException(
                status.MotivoBloqueio ?? "Seu plano atual não permite registrar novos lançamentos agora.",
                403);
        }
    }

    public async Task<BillingStatusDto> ObterStatusAtualAsync(CancellationToken cancellationToken = default)
    {
        var usuarioId = _currentUserContext.UsuarioId
            ?? throw new InvalidOperationException("Não existe usuário autenticado no contexto atual.");

        var assinatura = await _assinaturas.GetByUsuarioIdAsync(usuarioId, cancellationToken)
            ?? throw new InvalidOperationException("Assinatura do usuário não encontrada.");

        var utcNow = DateTime.UtcNow;
        var effectivePlan = ResolveEffectivePlan(assinatura, utcNow);
        var lancamentosNoMes = await CountCurrentMonthLaunchesAsync(cancellationToken);
        var freeLimit = _billingOptions.Value.FreeMonthlyLaunchLimit > 0
            ? _billingOptions.Value.FreeMonthlyLaunchLimit
            : 50;

        bool podeRegistrarLancamento = effectivePlan == PlanoUsuario.Premium || lancamentosNoMes < freeLimit;
        string? motivoBloqueio = podeRegistrarLancamento
            ? null
            : $"Você atingiu o limite de {freeLimit} lançamentos do plano Free neste mês. Faça upgrade para continuar.";

        return new BillingStatusDto(
            usuarioId,
            assinatura.PlanoAtual.ToString(),
            effectivePlan.ToString(),
            assinatura.StatusAssinatura.ToString(),
            assinatura.PremiumAteUtc,
            assinatura.TrialAteUtc,
            lancamentosNoMes,
            effectivePlan == PlanoUsuario.Premium ? null : freeLimit,
            podeRegistrarLancamento,
            motivoBloqueio);
    }

    private async Task<int> CountCurrentMonthLaunchesAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var startOfMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonthExclusive = startOfMonth.AddMonths(1);

        var totalTransacoes = await _transacoes.CountInPeriodAsync(startOfMonth, endOfMonthExclusive, cancellationToken);
        var totalReceitas = await _receitas.CountInPeriodAsync(startOfMonth, endOfMonthExclusive, cancellationToken);
        return totalTransacoes + totalReceitas;
    }

    private static PlanoUsuario ResolveEffectivePlan(AssinaturaUsuario assinatura, DateTime utcNow)
    {
        if (assinatura.TrialAteUtc.HasValue && assinatura.TrialAteUtc.Value > utcNow)
        {
            return PlanoUsuario.Premium;
        }

        if (assinatura.PlanoAtual == PlanoUsuario.Premium &&
            assinatura.StatusAssinatura == StatusAssinatura.Ativa &&
            (!assinatura.PremiumAteUtc.HasValue || assinatura.PremiumAteUtc.Value > utcNow))
        {
            return PlanoUsuario.Premium;
        }

        return PlanoUsuario.Free;
    }
}
