using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;

namespace FinanceBot.Application.Services;

public sealed class FinanceOperationsService : IFinanceOperationsService
{
    private readonly ITransacaoRepository _transacoes;
    private readonly IReceitaRepository _receitas;
    private readonly IFinanceUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAccessPolicyService _accessPolicyService;
    private readonly IGastoCategorizationService _gastoCategorizationService;

    public FinanceOperationsService(
        ITransacaoRepository transacoes,
        IReceitaRepository receitas,
        IFinanceUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IAccessPolicyService accessPolicyService,
        IGastoCategorizationService gastoCategorizationService)
    {
        _transacoes = transacoes;
        _receitas = receitas;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _accessPolicyService = accessPolicyService;
        _gastoCategorizationService = gastoCategorizationService;
    }

    public async Task<GastoDto> RegistrarGastoAsync(CriarGastoRequest request, CancellationToken cancellationToken = default)
    {
        var usuarioId = GetCurrentUserId();
        await _accessPolicyService.EnsureCanRegisterLancamentoAsync(cancellationToken);
        var transacao = new Transacao
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Descricao = request.Descricao.Trim(),
            Valor = request.Valor,
            Data = DateTime.UtcNow,
            Categoria = _gastoCategorizationService.Categorize(request.Descricao)
        };

        await _transacoes.AddAsync(transacao, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(transacao);
    }

    public async Task<ReceitaDto> RegistrarReceitaAsync(CriarReceitaRequest request, CancellationToken cancellationToken = default)
    {
        var usuarioId = GetCurrentUserId();
        await _accessPolicyService.EnsureCanRegisterLancamentoAsync(cancellationToken);
        var receita = new Receita
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Descricao = request.Descricao.Trim(),
            Valor = request.Valor,
            Data = DateTime.UtcNow,
            EhFixo = request.EhFixo
        };

        await _receitas.AddAsync(receita, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(receita);
    }

    public async Task<IReadOnlyList<GastoDto>> ListarGastosAsync(int limite = 20, CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var gastos = await _transacoes.ListRecentAsync(NormalizeTake(limite, 20), cancellationToken);
        return gastos.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ReceitaDto>> ListarReceitasAsync(int limite = 20, CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var receitas = await _receitas.ListRecentAsync(NormalizeTake(limite, 20), cancellationToken);
        return receitas.Select(Map).ToList();
    }

    public async Task<ResumoFinanceiroDto> ObterResumoAsync(DateOnly? data = null, CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var dataReferencia = data ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dataConsulta = dataReferencia.ToDateTime(TimeOnly.MinValue);

        var gastos = await _transacoes.SumByDateAsync(dataConsulta, cancellationToken);
        var ganhos = await _receitas.SumByDateAsync(dataConsulta, cancellationToken);

        return new ResumoFinanceiroDto(dataReferencia, ganhos, gastos, ganhos - gastos);
    }

    public async Task<RelatorioMensalDto> ObterRelatorioMensalAsync(
        int? ano = null,
        int? mes = null,
        CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        await _accessPolicyService.EnsureCanUsePremiumFeatureAsync("relatório mensal", cancellationToken);

        var now = DateTime.UtcNow;
        var year = ano ?? now.Year;
        var month = mes ?? now.Month;
        var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endExclusiveUtc = startUtc.AddMonths(1);

        var gastos = await _transacoes.ListInPeriodAsync(startUtc, endExclusiveUtc, cancellationToken);
        var receitas = await _receitas.ListInPeriodAsync(startUtc, endExclusiveUtc, cancellationToken);

        var totalGastos = gastos.Sum(x => x.Valor);
        var totalReceitas = receitas.Sum(x => x.Valor);
        var topCategorias = gastos
            .GroupBy(gasto => gasto.Categoria ?? "Outros")
            .Select(group => new CategoriaResumoDto(
                group.Key,
                group.Sum(item => item.Valor),
                group.Count()))
            .OrderByDescending(item => item.TotalGasto)
            .Take(5)
            .ToList();

        return new RelatorioMensalDto(
            year,
            month,
            totalReceitas,
            totalGastos,
            totalReceitas - totalGastos,
            gastos.Count + receitas.Count,
            topCategorias);
    }

    public async Task<IReadOnlyList<MovimentoDto>> ListarUltimosMovimentosAsync(int limite = 5, CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var take = NormalizeTake(limite, 5);
        var ultimosGastos = await _transacoes.ListRecentAsync(take, cancellationToken);
        var ultimasReceitas = await _receitas.ListRecentAsync(take, cancellationToken);

        return ultimosGastos.Select(g => new MovimentoDto("Gasto", g.Descricao, g.Valor, g.Data, g.Categoria ?? "Outros"))
            .Union(ultimasReceitas.Select(r => new MovimentoDto("Receita", r.Descricao, r.Valor, r.Data, null)))
            .OrderByDescending(m => m.Data)
            .Take(take)
            .ToList();
    }

    public async Task<DesfazerMovimentoResult?> DesfazerUltimaAcaoAsync(CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var ultimoGasto = await _transacoes.GetLatestAsync(cancellationToken);
        var ultimaReceita = await _receitas.GetLatestAsync(cancellationToken);

        if (ultimoGasto is null && ultimaReceita is null)
        {
            return null;
        }

        if (ultimaReceita is not null && (ultimoGasto is null || ultimaReceita.Data > ultimoGasto.Data))
        {
            _receitas.Remove(ultimaReceita);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new DesfazerMovimentoResult("Receita", ultimaReceita.Descricao);
        }

        var gastoParaRemover = ultimoGasto!;
        _transacoes.Remove(gastoParaRemover);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new DesfazerMovimentoResult("Gasto", gastoParaRemover.Descricao);
    }

    private static int NormalizeTake(int requestedTake, int defaultTake, int maxTake = 100)
    {
        if (requestedTake <= 0)
        {
            return defaultTake;
        }

        return Math.Min(requestedTake, maxTake);
    }

    private static GastoDto Map(Transacao transacao) =>
        new(transacao.Id, transacao.Descricao, transacao.Valor, transacao.Data, transacao.Categoria ?? "Outros");

    private static ReceitaDto Map(Receita receita) =>
        new(receita.Id, receita.Descricao, receita.Valor, receita.Data, receita.EhFixo);

    private Guid GetCurrentUserId()
    {
        return _currentUserContext.UsuarioId
            ?? throw new InvalidOperationException("Não existe usuário autenticado no contexto atual.");
    }
}
