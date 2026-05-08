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
    private readonly IFinanceUnitOfWork _unitOfWork;
    private readonly IOptions<BillingOptions> _billingOptions;

    public AccessPolicyService(
        ICurrentUserContext currentUserContext,
        IAssinaturaUsuarioRepository assinaturas,
        ITransacaoRepository transacoes,
        IReceitaRepository receitas,
        IFinanceUnitOfWork unitOfWork,
        IOptions<BillingOptions> billingOptions)
    {
        _currentUserContext = currentUserContext;
        _assinaturas = assinaturas;
        _transacoes = transacoes;
        _receitas = receitas;
        _unitOfWork = unitOfWork;
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

    public async Task EnsureCanUsePremiumFeatureAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var status = await ObterStatusAtualAsync(cancellationToken);
        if (string.Equals(status.PlanoEfetivo, PlanoUsuario.Premium.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        var featureLabel = string.IsNullOrWhiteSpace(featureName) ? "Este recurso" : $"O recurso {featureName}";
        var upgradeMessage = status.MensagemUpgrade ?? "Faça upgrade para liberar este recurso.";
        throw new AccessPolicyException($"{featureLabel} está disponível apenas no Premium. {upgradeMessage}", 403);
    }

    public async Task<BillingStatusDto> ObterStatusAtualAsync(CancellationToken cancellationToken = default)
    {
        var (usuarioId, assinatura) = await GetUsuarioEAssinaturaAsync(cancellationToken);
        var utcNow = DateTime.UtcNow;
        var effectivePlan = ResolveEffectivePlan(assinatura, utcNow);
        var lancamentosNoMes = await CountCurrentMonthLaunchesAsync(cancellationToken);
        var freeLimit = _billingOptions.Value.FreeMonthlyLaunchLimit > 0
            ? _billingOptions.Value.FreeMonthlyLaunchLimit
            : 50;
        var trialAtivo = assinatura.TrialAteUtc.HasValue && assinatura.TrialAteUtc.Value > utcNow;
        var upgradePendente = IsUpgradePending(assinatura);
        var premiumAtivo = effectivePlan == PlanoUsuario.Premium && assinatura.StatusAssinatura == StatusAssinatura.Ativa;
        var podeSolicitarUpgrade = !upgradePendente && !premiumAtivo;
        int? diasRestantesTrial = trialAtivo
            ? Math.Max(1, (int)Math.Ceiling((assinatura.TrialAteUtc!.Value - utcNow).TotalDays))
            : null;

        bool podeRegistrarLancamento = effectivePlan == PlanoUsuario.Premium || lancamentosNoMes < freeLimit;
        var mensagemUpgrade = BuildUpgradeMessage(
            effectivePlan,
            trialAtivo,
            diasRestantesTrial,
            podeRegistrarLancamento,
            upgradePendente,
            podeSolicitarUpgrade);
        var mensagemStatus = BuildStatusMessage(
            effectivePlan,
            freeLimit,
            lancamentosNoMes,
            podeRegistrarLancamento,
            trialAtivo,
            diasRestantesTrial,
            upgradePendente);
        string? motivoBloqueio = podeRegistrarLancamento
            ? null
            : JoinMessages(
                mensagemStatus,
                mensagemUpgrade ?? "Faça upgrade para continuar registrando lançamentos.");

        return new BillingStatusDto(
            usuarioId,
            assinatura.PlanoAtual.ToString(),
            effectivePlan.ToString(),
            assinatura.StatusAssinatura.ToString(),
            assinatura.PremiumAteUtc,
            assinatura.TrialAteUtc,
            assinatura.UpgradeSolicitadoEmUtc,
            lancamentosNoMes,
            effectivePlan == PlanoUsuario.Premium ? null : (int?)freeLimit,
            podeRegistrarLancamento,
            upgradePendente,
            podeSolicitarUpgrade,
            motivoBloqueio,
            trialAtivo,
            diasRestantesTrial,
            mensagemStatus,
            mensagemUpgrade);
    }

    public async Task<SolicitacaoUpgradeDto> SolicitarUpgradeAsync(CancellationToken cancellationToken = default)
    {
        var (_, assinatura) = await GetUsuarioEAssinaturaAsync(cancellationToken);
        var utcNow = DateTime.UtcNow;
        var effectivePlan = ResolveEffectivePlan(assinatura, utcNow);
        var premiumAtivo = effectivePlan == PlanoUsuario.Premium && assinatura.StatusAssinatura == StatusAssinatura.Ativa;

        if (premiumAtivo)
        {
            return new SolicitacaoUpgradeDto(
                true,
                "Seu acesso Premium já está ativo. Não há necessidade de pedir upgrade agora.",
                assinatura.UpgradeSolicitadoEmUtc,
                false);
        }

        if (IsUpgradePending(assinatura))
        {
            return new SolicitacaoUpgradeDto(
                true,
                "Seu pedido de upgrade já foi registrado e está aguardando ativação.",
                assinatura.UpgradeSolicitadoEmUtc,
                true);
        }

        assinatura.StatusAssinatura = StatusAssinatura.Pendente;
        assinatura.UpgradeSolicitadoEmUtc = utcNow;
        assinatura.AtualizadoEmUtc = utcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mensagem = effectivePlan == PlanoUsuario.Premium
            ? "Pedido de upgrade registrado. Seu trial continua ativo enquanto a ativação do Premium não é concluída."
            : "Pedido de upgrade registrado. Sua conta agora fica marcada para ativação do Premium assim que essa etapa estiver disponível.";

        return new SolicitacaoUpgradeDto(
            true,
            mensagem,
            assinatura.UpgradeSolicitadoEmUtc,
            true);
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

    private static string BuildStatusMessage(
        PlanoUsuario effectivePlan,
        int freeLimit,
        int lancamentosNoMes,
        bool podeRegistrarLancamento,
        bool trialAtivo,
        int? diasRestantesTrial,
        bool upgradePendente)
    {
        if (upgradePendente)
        {
            if (trialAtivo)
            {
                return diasRestantesTrial == 1
                    ? "Seu upgrade para o Premium foi solicitado. Seu trial continua ativo e termina em 1 dia."
                    : $"Seu upgrade para o Premium foi solicitado. Seu trial continua ativo e termina em {diasRestantesTrial} dias.";
            }

            return "Seu upgrade para o Premium foi solicitado e está aguardando ativação.";
        }

        if (trialAtivo)
        {
            return diasRestantesTrial == 1
                ? "Seu trial Premium está ativo e termina em 1 dia."
                : $"Seu trial Premium está ativo e termina em {diasRestantesTrial} dias.";
        }

        if (effectivePlan == PlanoUsuario.Premium)
        {
            return "Seu acesso Premium está ativo.";
        }

        if (!podeRegistrarLancamento)
        {
            return $"Você atingiu o limite de {freeLimit} lançamentos do plano Free neste mês.";
        }

        var restantes = Math.Max(0, freeLimit - lancamentosNoMes);
        return restantes == 1
            ? "Você está no plano Free com 1 lançamento restante neste mês."
            : $"Você está no plano Free com {restantes} lançamentos restantes neste mês.";
    }

    private static string? BuildUpgradeMessage(
        PlanoUsuario effectivePlan,
        bool trialAtivo,
        int? diasRestantesTrial,
        bool podeRegistrarLancamento,
        bool upgradePendente,
        bool podeSolicitarUpgrade)
    {
        if (upgradePendente)
        {
            return "Seu pedido de upgrade já foi registrado. Quando a ativação do Premium estiver disponível, este fluxo poderá ser concluído sem refazer a solicitação.";
        }

        if (trialAtivo && diasRestantesTrial <= 2)
        {
            return "Solicite seu upgrade para não perder o acesso Premium quando o trial acabar.";
        }

        if (effectivePlan == PlanoUsuario.Premium)
        {
            return null;
        }

        if (!podeRegistrarLancamento)
        {
            return "Solicite o upgrade para liberar lançamentos ilimitados e continuar usando os recursos premium.";
        }

        return podeSolicitarUpgrade
            ? "Você já pode solicitar o upgrade para liberar lançamentos ilimitados e futuras funções premium."
            : null;
    }

    private static string JoinMessages(string first, string second)
    {
        return $"{first} {second}".Trim();
    }

    private async Task<(Guid UsuarioId, AssinaturaUsuario Assinatura)> GetUsuarioEAssinaturaAsync(CancellationToken cancellationToken)
    {
        var usuarioId = _currentUserContext.UsuarioId
            ?? throw new InvalidOperationException("Não existe usuário autenticado no contexto atual.");

        var assinatura = await _assinaturas.GetByUsuarioIdAsync(usuarioId, cancellationToken)
            ?? throw new InvalidOperationException("Assinatura do usuário não encontrada.");

        return (usuarioId, assinatura);
    }

    private static bool IsUpgradePending(AssinaturaUsuario assinatura)
    {
        return assinatura.StatusAssinatura == StatusAssinatura.Pendente &&
               assinatura.UpgradeSolicitadoEmUtc.HasValue;
    }
}
