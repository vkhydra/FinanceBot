using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;

namespace FinanceBot.Application.Services;

public sealed class FinanceOperationsService : IFinanceOperationsService
{
    private readonly ITransacaoRepository _transacoes;
    private readonly IReceitaRepository _receitas;
    private readonly IFinanceUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAccessPolicyService _accessPolicyService;
    private readonly IGastoCategorizationService _gastoCategorizationService;
    private readonly IOrcamentoMensalRepository _orcamentosMensais;

    public FinanceOperationsService(
        ITransacaoRepository transacoes,
        IReceitaRepository receitas,
        IOrcamentoMensalRepository orcamentosMensais,
        IFinanceUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IAccessPolicyService accessPolicyService,
        IGastoCategorizationService gastoCategorizationService)
    {
        _transacoes = transacoes;
        _receitas = receitas;
        _orcamentosMensais = orcamentosMensais;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _accessPolicyService = accessPolicyService;
        _gastoCategorizationService = gastoCategorizationService;
    }

    public async Task<GastoDto> RegistrarGastoAsync(CriarGastoRequest request, CancellationToken cancellationToken = default)
    {
        var usuarioId = GetCurrentUserId();
        await _accessPolicyService.EnsureCanRegisterLancamentoAsync(cancellationToken);
        var classification = _gastoCategorizationService.Classify(request.Descricao);
        var transacao = new Transacao
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Descricao = request.Descricao.Trim(),
            Valor = request.Valor,
            Data = DateTime.UtcNow,
            Categoria = classification.Categoria,
            EhFixo = request.EhFixo ?? classification.EhFixo,
            EhEssencial = request.EhEssencial ?? classification.EhEssencial,
            Observacao = NormalizeObservacao(request.Observacao),
            Origem = ResolveOrigem()
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
            EhFixo = request.EhFixo,
            Observacao = NormalizeObservacao(request.Observacao),
            Origem = ResolveOrigem()
        };

        await _receitas.AddAsync(receita, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(receita);
    }

    public async Task<GastoDto?> AtualizarGastoAsync(
        Guid gastoId,
        AtualizarGastoRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var gasto = await _transacoes.GetByIdAsync(gastoId, cancellationToken);
        if (gasto is null)
        {
            return null;
        }

        var classification = _gastoCategorizationService.Classify(request.Descricao);
        gasto.Descricao = request.Descricao.Trim();
        gasto.Valor = request.Valor;
        gasto.Data = CombineDateWithExistingTime(gasto.Data, request.Data);
        gasto.Categoria = string.IsNullOrWhiteSpace(request.Categoria)
            ? classification.Categoria
            : request.Categoria.Trim();
        gasto.EhFixo = request.EhFixo ?? gasto.EhFixo;
        gasto.EhEssencial = request.EhEssencial ?? gasto.EhEssencial;
        gasto.Observacao = NormalizeObservacao(request.Observacao);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(gasto);
    }

    public async Task<ReceitaDto?> AtualizarReceitaAsync(
        Guid receitaId,
        AtualizarReceitaRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var receita = await _receitas.GetByIdAsync(receitaId, cancellationToken);
        if (receita is null)
        {
            return null;
        }

        receita.Descricao = request.Descricao.Trim();
        receita.Valor = request.Valor;
        receita.Data = CombineDateWithExistingTime(receita.Data, request.Data);
        receita.EhFixo = request.EhFixo;
        receita.Observacao = NormalizeObservacao(request.Observacao);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(receita);
    }

    public async Task<bool> ExcluirGastoAsync(Guid gastoId, CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var gasto = await _transacoes.GetByIdAsync(gastoId, cancellationToken);
        if (gasto is null)
        {
            return false;
        }

        _transacoes.Remove(gasto);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExcluirReceitaAsync(Guid receitaId, CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var receita = await _receitas.GetByIdAsync(receitaId, cancellationToken);
        if (receita is null)
        {
            return false;
        }

        _receitas.Remove(receita);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
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

    public async Task<IReadOnlyList<MovimentoDto>> ListarMovimentosAsync(
        ListarMovimentosRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();

        var take = NormalizeTake(request.Limite, 100, 200);
        var sourceTake = NormalizeTake(Math.Max(take * 2, 100), 100, 400);
        var (startUtc, endExclusiveUtc) = BuildPeriodo(request.Inicio, request.Fim);

        IReadOnlyList<Transacao> gastos;
        IReadOnlyList<Receita> receitas;

        if (startUtc.HasValue && endExclusiveUtc.HasValue)
        {
            gastos = await _transacoes.ListInPeriodAsync(startUtc.Value, endExclusiveUtc.Value, cancellationToken);
            receitas = await _receitas.ListInPeriodAsync(startUtc.Value, endExclusiveUtc.Value, cancellationToken);
        }
        else
        {
            gastos = await _transacoes.ListRecentAsync(sourceTake, cancellationToken);
            receitas = await _receitas.ListRecentAsync(sourceTake, cancellationToken);
        }

        var movimentos = gastos.Select(MapMovimento)
            .Concat(receitas.Select(MapMovimento))
            .Where(movimento => MatchesTipo(movimento, request.Tipo))
            .Where(movimento => MatchesBusca(movimento, request.Busca))
            .Where(movimento => MatchesCategoria(movimento, request.Categoria))
            .Where(movimento => MatchesOrigem(movimento, request.Origem))
            .OrderByDescending(movimento => movimento.Data)
            .Take(take)
            .ToList();

        return movimentos;
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

    public async Task<OrcamentoMensalDto> ObterOrcamentoMensalAsync(
        int? ano = null,
        int? mes = null,
        CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var (year, month, startUtc, endExclusiveUtc) = ResolveMonthlyPeriod(ano, mes);
        var orcamento = await _orcamentosMensais.GetByPeriodoAsync(year, month, cancellationToken);
        var gastos = await _transacoes.ListInPeriodAsync(startUtc, endExclusiveUtc, cancellationToken);
        var receitas = await _receitas.ListInPeriodAsync(startUtc, endExclusiveUtc, cancellationToken);
        var historico = await LoadBudgetHistoryAsync(year, month, cancellationToken);

        return BuildBudgetDto(year, month, orcamento, gastos, receitas, historico);
    }

    public async Task<OrcamentoMensalDto> AtualizarOrcamentoMensalAsync(
        AtualizarOrcamentoMensalRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = GetCurrentUserId();
        var (year, month, startUtc, endExclusiveUtc) = ResolveMonthlyPeriod(request.Ano, request.Mes);
        var orcamento = await _orcamentosMensais.GetByPeriodoAsync(year, month, cancellationToken);

        if (orcamento is null)
        {
            orcamento = new OrcamentoMensal
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Ano = year,
                Mes = month,
                LimiteGastos = request.LimiteGastos,
                AtualizadoEmUtc = DateTime.UtcNow
            };

            await _orcamentosMensais.AddAsync(orcamento, cancellationToken);
        }
        else
        {
            orcamento.LimiteGastos = request.LimiteGastos;
            orcamento.AtualizadoEmUtc = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var gastos = await _transacoes.ListInPeriodAsync(startUtc, endExclusiveUtc, cancellationToken);
        var receitas = await _receitas.ListInPeriodAsync(startUtc, endExclusiveUtc, cancellationToken);
        var historico = await LoadBudgetHistoryAsync(year, month, cancellationToken);

        return BuildBudgetDto(year, month, orcamento, gastos, receitas, historico);
    }

    public async Task<IReadOnlyList<MovimentoDto>> ListarUltimosMovimentosAsync(int limite = 5, CancellationToken cancellationToken = default)
    {
        _ = GetCurrentUserId();
        var take = NormalizeTake(limite, 5);
        var ultimosGastos = await _transacoes.ListRecentAsync(take, cancellationToken);
        var ultimasReceitas = await _receitas.ListRecentAsync(take, cancellationToken);

        return ultimosGastos.Select(MapMovimento)
            .Union(ultimasReceitas.Select(MapMovimento))
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
        new(
            transacao.Id,
            transacao.Descricao,
            transacao.Valor,
            transacao.Data,
            transacao.Categoria ?? "Outros",
            transacao.EhFixo,
            transacao.EhEssencial,
            transacao.Origem.ToString(),
            transacao.Observacao);

    private static ReceitaDto Map(Receita receita) =>
        new(
            receita.Id,
            receita.Descricao,
            receita.Valor,
            receita.Data,
            receita.EhFixo,
            receita.Origem.ToString(),
            receita.Observacao);

    private static MovimentoDto MapMovimento(Transacao transacao) =>
        new(
            transacao.Id,
            "Gasto",
            transacao.Descricao,
            transacao.Valor,
            transacao.Data,
            transacao.Categoria ?? "Outros",
            transacao.EhFixo,
            transacao.EhEssencial,
            transacao.Origem.ToString(),
            transacao.Observacao);

    private static MovimentoDto MapMovimento(Receita receita) =>
        new(
            receita.Id,
            "Receita",
            receita.Descricao,
            receita.Valor,
            receita.Data,
            null,
            receita.EhFixo,
            null,
            receita.Origem.ToString(),
            receita.Observacao);

    private static OrcamentoMensalDto BuildBudgetDto(
        int ano,
        int mes,
        OrcamentoMensal? orcamento,
        IReadOnlyList<Transacao> gastos,
        IReadOnlyList<Receita> receitas,
        IReadOnlyList<BudgetHistorySnapshot> historico)
    {
        var totalGastos = gastos.Sum(item => item.Valor);
        var totalReceitas = receitas.Sum(item => item.Valor);
        var gastoFixo = gastos.Where(item => item.EhFixo).Sum(item => item.Valor);
        var gastoEssencial = gastos.Where(item => item.EhEssencial).Sum(item => item.Valor);
        var gastoNaoEssencial = totalGastos - gastoEssencial;
        var limite = orcamento?.LimiteGastos;
        var diasNoMes = DateTime.DaysInMonth(ano, mes);
        var hojeUtc = DateTime.UtcNow.Date;
        var competenciaAtual = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var mesAtual = new DateTime(hojeUtc.Year, hojeUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        int diasDecorridos;
        int diasRestantes;
        decimal projecaoFechamento;

        if (competenciaAtual == mesAtual)
        {
            diasDecorridos = Math.Min(hojeUtc.Day, diasNoMes);
            diasRestantes = Math.Max(diasNoMes - diasDecorridos, 0);
            projecaoFechamento = diasDecorridos > 0
                ? decimal.Round((totalGastos / diasDecorridos) * diasNoMes, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }
        else if (competenciaAtual < mesAtual)
        {
            diasDecorridos = diasNoMes;
            diasRestantes = 0;
            projecaoFechamento = totalGastos;
        }
        else
        {
            diasDecorridos = 0;
            diasRestantes = diasNoMes;
            projecaoFechamento = 0m;
        }

        decimal? restante = limite.HasValue ? limite.Value - totalGastos : null;
        decimal? percentualConsumido = limite.HasValue && limite.Value > 0 ? totalGastos / limite.Value : null;
        decimal? diferencaProjetada = limite.HasValue ? limite.Value - projecaoFechamento : null;
        var estourado = limite.HasValue && totalGastos > limite.Value;
        var estouroProjetado = limite.HasValue && projecaoFechamento > limite.Value;
        var sugestoes = BuildBudgetSuggestions(
            new BudgetHistorySnapshot(totalGastos, totalReceitas, gastoFixo, gastoEssencial, gastoNaoEssencial, orcamento is not null),
            historico);

        return new OrcamentoMensalDto(
            ano,
            mes,
            limite,
            totalGastos,
            totalReceitas,
            gastoFixo,
            gastoEssencial,
            gastoNaoEssencial,
            restante,
            percentualConsumido,
            projecaoFechamento,
            diferencaProjetada,
            diasNoMes,
            diasDecorridos,
            diasRestantes,
            orcamento is not null,
            estourado,
            estouroProjetado,
            sugestoes.Seguro,
            sugestoes.Equilibrado,
            sugestoes.Flexivel,
            sugestoes.MesesBase);
    }

    private async Task<IReadOnlyList<BudgetHistorySnapshot>> LoadBudgetHistoryAsync(
        int ano,
        int mes,
        CancellationToken cancellationToken)
    {
        var referencia = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var historico = new List<BudgetHistorySnapshot>(3);

        for (var offset = 1; offset <= 3; offset++)
        {
            var periodo = referencia.AddMonths(-offset);
            var (historyYear, historyMonth, startUtc, endExclusiveUtc) = ResolveMonthlyPeriod(periodo.Year, periodo.Month);
            var orcamento = await _orcamentosMensais.GetByPeriodoAsync(historyYear, historyMonth, cancellationToken);
            var gastos = await _transacoes.ListInPeriodAsync(startUtc, endExclusiveUtc, cancellationToken);
            var receitas = await _receitas.ListInPeriodAsync(startUtc, endExclusiveUtc, cancellationToken);

            historico.Add(new BudgetHistorySnapshot(
                gastos.Sum(item => item.Valor),
                receitas.Sum(item => item.Valor),
                gastos.Where(item => item.EhFixo).Sum(item => item.Valor),
                gastos.Where(item => item.EhEssencial).Sum(item => item.Valor),
                gastos.Where(item => !item.EhEssencial).Sum(item => item.Valor),
                orcamento is not null));
        }

        return historico;
    }

    private static BudgetSuggestions BuildBudgetSuggestions(
        BudgetHistorySnapshot atual,
        IReadOnlyList<BudgetHistorySnapshot> historico)
    {
        var baseHistorica = historico
            .Where(item =>
                item.TotalGastos > 0m ||
                item.TotalReceitas > 0m ||
                item.GastoFixo > 0m ||
                item.GastoEssencial > 0m ||
                item.GastoNaoEssencial > 0m ||
                item.PossuiOrcamentoDefinido)
            .ToList();

        if (baseHistorica.Count == 0)
        {
            baseHistorica.Add(atual);
        }

        var mediaFixo = baseHistorica.Average(item => item.GastoFixo);
        var mediaEssencial = baseHistorica.Average(item => item.GastoEssencial);
        var mediaNaoEssencial = baseHistorica.Average(item => item.GastoNaoEssencial);
        var receitasPositivas = baseHistorica.Where(item => item.TotalReceitas > 0m).Select(item => item.TotalReceitas).ToList();
        var mediaReceitas = receitasPositivas.Count > 0 ? receitasPositivas.Average() : 0m;
        var baseComprometida = Math.Max(mediaFixo, mediaEssencial);
        decimal? travaSegura = mediaReceitas > 0m ? mediaReceitas * 0.8m : null;
        decimal? travaFlexivel = mediaReceitas > 0m ? mediaReceitas * 0.85m : null;

        return new BudgetSuggestions(
            ApplyBudgetCap(baseComprometida * 1.05m, baseComprometida, travaSegura),
            ApplyBudgetCap(baseComprometida + (mediaNaoEssencial * 0.5m), baseComprometida, travaSegura),
            ApplyBudgetCap(baseComprometida + (mediaNaoEssencial * 0.8m), baseComprometida, travaFlexivel),
            historico.Count(item =>
                item.TotalGastos > 0m ||
                item.TotalReceitas > 0m ||
                item.GastoFixo > 0m ||
                item.GastoEssencial > 0m ||
                item.GastoNaoEssencial > 0m ||
                item.PossuiOrcamentoDefinido));
    }

    private static decimal ApplyBudgetCap(decimal alvo, decimal piso, decimal? trava)
    {
        var normalizado = decimal.Max(alvo, piso);
        var comTrava = trava.HasValue ? decimal.Min(normalizado, trava.Value) : normalizado;
        return decimal.Round(decimal.Max(comTrava, piso), 2, MidpointRounding.AwayFromZero);
    }

    private static (DateTime? StartUtc, DateTime? EndExclusiveUtc) BuildPeriodo(DateOnly? inicio, DateOnly? fim)
    {
        if (!inicio.HasValue && !fim.HasValue)
        {
            return (null, null);
        }

        var startDate = inicio ?? fim!.Value;
        var endDate = fim ?? inicio!.Value;

        var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusiveUtc = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return (startUtc, endExclusiveUtc);
    }

    private static bool MatchesTipo(MovimentoDto movimento, string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
        {
            return true;
        }

        return string.Equals(movimento.Tipo, tipo.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesBusca(MovimentoDto movimento, string? busca)
    {
        if (string.IsNullOrWhiteSpace(busca))
        {
            return true;
        }

        return movimento.Descricao.Contains(busca.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesCategoria(MovimentoDto movimento, string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
        {
            return true;
        }

        return string.Equals(movimento.Categoria, categoria.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesOrigem(MovimentoDto movimento, string? origem)
    {
        if (string.IsNullOrWhiteSpace(origem))
        {
            return true;
        }

        return string.Equals(movimento.Origem, origem.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime CombineDateWithExistingTime(DateTime existingUtc, DateOnly date)
    {
        var utc = existingUtc.Kind == DateTimeKind.Utc
            ? existingUtc
            : DateTime.SpecifyKind(existingUtc, DateTimeKind.Utc);

        var time = utc.TimeOfDay;
        return new DateTime(date.Year, date.Month, date.Day, time.Hours, time.Minutes, time.Seconds, DateTimeKind.Utc)
            .AddTicks(time.Ticks % TimeSpan.TicksPerSecond);
    }

    private static string? NormalizeObservacao(string? observacao)
    {
        return string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }

    private static (int Ano, int Mes, DateTime StartUtc, DateTime EndExclusiveUtc) ResolveMonthlyPeriod(int? ano, int? mes)
    {
        var now = DateTime.UtcNow;
        var year = ano ?? now.Year;
        var month = mes ?? now.Month;
        var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (year, month, startUtc, startUtc.AddMonths(1));
    }

    private sealed record BudgetHistorySnapshot(
        decimal TotalGastos,
        decimal TotalReceitas,
        decimal GastoFixo,
        decimal GastoEssencial,
        decimal GastoNaoEssencial,
        bool PossuiOrcamentoDefinido);

    private sealed record BudgetSuggestions(
        decimal Seguro,
        decimal Equilibrado,
        decimal Flexivel,
        int MesesBase);

    private OrigemLancamento ResolveOrigem()
    {
        return _currentUserContext.TelegramChatId.HasValue
            ? OrigemLancamento.Telegram
            : OrigemLancamento.Web;
    }

    private Guid GetCurrentUserId()
    {
        return _currentUserContext.UsuarioId
            ?? throw new InvalidOperationException("Não existe usuário autenticado no contexto atual.");
    }
}
