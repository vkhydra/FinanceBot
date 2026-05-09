using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using FinanceBot.Application.Contracts;
namespace FinanceBot.Application.Services;

public sealed class FinanceMessageProcessor : IFinanceMessageProcessor
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Regex NumericValueRegex = new(@"\d+(?:[.,]\d{1,2})?", RegexOptions.Compiled);
    private readonly IFinanceOperationsService _financeOperationsService;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAccessPolicyService _accessPolicyService;

    public FinanceMessageProcessor(
        IFinanceOperationsService financeOperationsService,
        IIdentityService identityService,
        ICurrentUserContext currentUserContext,
        IAccessPolicyService accessPolicyService)
    {
        _financeOperationsService = financeOperationsService;
        _identityService = identityService;
        _currentUserContext = currentUserContext;
        _accessPolicyService = accessPolicyService;
    }

    public async Task<FinanceMessageResult> ProcessarMensagemAsync(FinanceMessageRequest request, CancellationToken cancellationToken = default)
    {
        string mensagem = request.CorpoMensagem.Trim();
        string comando = mensagem.ToLowerInvariant();
        string comandoNormalizado = comando.TrimStart('/');

        if (TryParseLinkCommand(comandoNormalizado, out var codigoVinculo))
        {
            var resultadoVinculo = await _identityService.VincularTelegramAsync(request.ChatId, codigoVinculo, cancellationToken);
            return FormatarResultadoVinculo(resultadoVinculo);
        }

        if (IsUnlinkCommand(comandoNormalizado))
        {
            var resultadoDesvinculo = await _identityService.DesvincularTelegramAsync(request.ChatId, cancellationToken);
            return FormatarResultadoDesvinculo(resultadoDesvinculo);
        }

        if (_currentUserContext.UsuarioId is null)
        {
            return IsHelpLikeCommand(comandoNormalizado)
                ? ObterMenuVinculo()
                : FinanceMessageResult.FalhaHtml(BuildMessage(
                    "🔒 <b>Este chat ainda não está conectado</b>",
                    "Antes de registrar gastos e receitas por aqui, você precisa ligar este Telegram à sua conta do FinanceBot.",
                    BuildLinkInstructions(),
                    $"Depois disso, você já pode enviar {Code("cafe 8,50")} ou {Code("+ freelance 350")}.",
                    $"Se quiser, envie {Code("ajuda")} e eu mostro tudo resumido."));
        }

        return comandoNormalizado switch
        {
            "total" or "resumo" => await ObterResumoFinanceiro(cancellationToken),
            "orcamento" or "orçamento" => await ObterOrcamentoMensal(cancellationToken),
            "listar" or "movimentos" => await ListarUltimosRegistros(cancellationToken),
            "relatorio" or "relatório" => await ObterRelatorioMensal(cancellationToken),
            "upgrade" or "assinar" => await SolicitarUpgrade(cancellationToken),
            "desfazer" => await DesfazerUltimaAcao(cancellationToken),
            "plano" or "status" => await ObterStatusPlano(cancellationToken),
            "comandos" or "ajuda" or "help" or "menu" or "start" or "oi" or "ola" or "olá" or "comecar" or "começar" => ObterMenuAjuda(),
            _ => await IdentificarERegistrar(mensagem, cancellationToken)
        };
    }

    private static (bool Valido, string Descricao, decimal Valor, bool EhFixo) ParseDados(string mensagem)
    {
        bool ehFixo = mensagem.EndsWith("fixo", StringComparison.OrdinalIgnoreCase);
        string texto = ehFixo ? mensagem[..^4].Trim() : mensagem.Trim();

        int ultimoEspaco = texto.LastIndexOf(' ');
        if (ultimoEspaco <= 0 || ultimoEspaco == texto.Length - 1)
        {
            return (false, "", 0, false);
        }

        string descricao = texto[..ultimoEspaco].Trim();
        string valorStr = texto[(ultimoEspaco + 1)..].Replace(",", ".");

        if (string.IsNullOrWhiteSpace(descricao))
        {
            return (false, "", 0, false);
        }

        if (decimal.TryParse(valorStr, CultureInfo.InvariantCulture, out decimal valor))
        {
            return (true, descricao, valor, ehFixo);
        }

        return (false, "", 0, false);
    }

    private async Task<FinanceMessageResult> RegistrarGasto(string mensagem, CancellationToken cancellationToken)
    {
        var (valido, desc, valor, ehFixo) = ParseDados(mensagem);
        if (!valido)
        {
            return FinanceMessageResult.FalhaHtml(BuildMessage(
                "⚠️ <b>Não consegui interpretar esse gasto</b>",
                "Use um destes formatos:",
                BuildBulletList(
                    Bullet(Code("Café 8,50")),
                    Bullet(Code("Uber 15")),
                    Bullet(Code("aluguel 1200 fixo")),
                    Bullet(Code("gastei 18 no uber")))));
        }

        GastoDto gasto;
        try
        {
            gasto = await _financeOperationsService.RegistrarGastoAsync(
                new CriarGastoRequest(desc, valor, EhFixo: ehFixo),
                cancellationToken);
        }
        catch (AccessPolicyException ex)
        {
            return FinanceMessageResult.FalhaHtml(BuildMessage(
                "🚫 <b>Não foi possível registrar o lançamento</b>",
                Encode(ex.Message),
                $"Consulte {Code("/plano")} para ver seu status atual."));
        }

        return FinanceMessageResult.OkHtml(BuildMessage(
            "✅ <b>Gasto registrado</b>",
            BuildBulletList(
                BulletField("Descrição", FormatDescription(gasto.Descricao)),
                BulletField("Valor", FormatCurrency(gasto.Valor)),
                BulletField("Categoria", gasto.Categoria),
                BulletField("Perfil", FormatGastoProfile(gasto.EhFixo, gasto.EhEssencial))),
            $"Próximo passo: consulte {Code("/resumo")} ou {Code("/movimentos")}."));
    }

    private async Task<FinanceMessageResult> RegistrarReceita(string mensagem, CancellationToken cancellationToken)
    {
        string limpa = mensagem.Replace("+", "").Replace("ganho", "", StringComparison.OrdinalIgnoreCase).Trim();

        var (valido, desc, valor, ehFixo) = ParseDados(limpa);
        if (!valido)
        {
            return FinanceMessageResult.FalhaHtml(BuildMessage(
                "⚠️ <b>Não consegui interpretar essa receita</b>",
                "Use um destes formatos:",
                BuildBulletList(
                    Bullet(Code("+ freelance 350")),
                    Bullet(Code("receita venda 120")),
                    Bullet(Code("+ salário 5000 fixo")),
                    Bullet(Code("recebi 350 do freelance")))));
        }

        ReceitaDto receita;
        try
        {
            receita = await _financeOperationsService.RegistrarReceitaAsync(
                new CriarReceitaRequest(desc, valor, ehFixo),
                cancellationToken);
        }
        catch (AccessPolicyException ex)
        {
            return FinanceMessageResult.FalhaHtml(BuildMessage(
                "🚫 <b>Não foi possível registrar o lançamento</b>",
                Encode(ex.Message),
                $"Consulte {Code("/plano")} para ver seu status atual."));
        }

        return FinanceMessageResult.OkHtml(BuildMessage(
            "✅ <b>Receita registrada</b>",
            BuildBulletList(
                BulletField("Descrição", FormatDescription(receita.Descricao)),
                BulletField("Valor", FormatCurrency(receita.Valor)),
                BulletField("Tipo", receita.EhFixo ? "Fixa" : "Variável")),
            $"Próximo passo: consulte {Code("/resumo")} ou {Code("/movimentos")}."));
    }

    private async Task<FinanceMessageResult> IdentificarERegistrar(string mensagem, CancellationToken cancellationToken)
    {
        string msg = mensagem.ToLowerInvariant();

        if (TryParseNaturalLancamento(mensagem, out var ehReceita, out var descricao, out var valor, out var ehFixo))
        {
            var normalizada = ehReceita
                ? $"+ {descricao} {valor.ToString("0.##", CultureInfo.InvariantCulture)}{(ehFixo ? " fixo" : string.Empty)}"
                : $"{descricao} {valor.ToString("0.##", CultureInfo.InvariantCulture)}";

            return ehReceita
                ? await RegistrarReceita(normalizada, cancellationToken)
                : await RegistrarGasto(normalizada, cancellationToken);
        }

        if (msg.StartsWith("+") || msg.StartsWith("ganho") || msg.StartsWith("receita"))
        {
            return await RegistrarReceita(mensagem, cancellationToken);
        }

        var resultado = await RegistrarGasto(mensagem, cancellationToken);
        if (!resultado.Sucesso)
        {
            return FinanceMessageResult.FalhaHtml(BuildMessage(
                "⚠️ <b>Não consegui interpretar a mensagem</b>",
                $"Tente algo como {Code("gastei 18 no uber")} ou {Code("+ freelance 350")}.",
                $"Envie {Code("ajuda")} para ver todos os comandos disponíveis."));
        }

        return resultado;
    }

    private async Task<FinanceMessageResult> ObterResumoFinanceiro(CancellationToken cancellationToken)
    {
        var resumo = await _financeOperationsService.ObterResumoAsync(cancellationToken: cancellationToken);
        var orcamento = await _financeOperationsService.ObterOrcamentoMensalAsync(cancellationToken: cancellationToken);

        return FinanceMessageResult.OkHtml(BuildMessage(
            "📊 <b>Resumo do dia</b>",
            BuildBulletList(
                BulletField("Entradas", FormatCurrency(resumo.Ganhos)),
                BulletField("Saídas", FormatCurrency(resumo.Gastos)),
                BulletField("Saldo", FormatCurrency(resumo.Saldo))),
            Encode(BuildBudgetMonthStatusLine(orcamento))));
    }

    private async Task<FinanceMessageResult> ObterOrcamentoMensal(CancellationToken cancellationToken)
    {
        var orcamento = await _financeOperationsService.ObterOrcamentoMensalAsync(cancellationToken: cancellationToken);
        var referenciaAnterior = new DateTime(orcamento.Ano, orcamento.Mes, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        var orcamentoAnterior = await _financeOperationsService.ObterOrcamentoMensalAsync(
            referenciaAnterior.Year,
            referenciaAnterior.Month,
            cancellationToken);
        var comparacaoAnterior = BuildBudgetMonthComparisonText(orcamento, orcamentoAnterior);
        var ritmoDiario = BuildBudgetDailyAllowanceText(orcamento);
        var compromissoBase = $"Os gastos fixos já comprometem {FormatCurrency(orcamento.GastoFixo)} e os essenciais {FormatCurrency(orcamento.GastoEssencial)}.";
        var sugestao = BuildBudgetSuggestionGuidanceText(orcamento);

        if (!orcamento.PossuiOrcamentoDefinido)
        {
            return FinanceMessageResult.OkHtml(BuildMessage(
                "🎯 <b>Orçamento do mês</b>",
                BuildBulletList(
                    BulletField("Referência", FormatBudgetReference(orcamento.Ano, orcamento.Mes)),
                    BulletField("Gastos acumulados", FormatCurrency(orcamento.TotalGastos)),
                    BulletField("Projeção", FormatCurrency(orcamento.ProjecaoFechamento)),
                    BulletField($"Vs {FormatBudgetReference(referenciaAnterior.Year, referenciaAnterior.Month)}", comparacaoAnterior),
                    BulletField("Fixos", FormatCurrency(orcamento.GastoFixo)),
                    BulletField("Essenciais", FormatCurrency(orcamento.GastoEssencial)),
                    BulletField("Sugestão equilibrada", FormatBudgetSuggestionValue(orcamento.SugestaoLimiteEquilibrado))),
                Encode(compromissoBase),
                Encode(sugestao),
                "Você ainda não definiu um limite mensal na Web. Faça isso no dashboard para eu passar a responder quanto ainda pode gastar."));
        }

        var status = orcamento.Estourado
            ? $"Você já ultrapassou o limite em {FormatCurrency(Math.Abs(orcamento.Restante ?? 0m))}."
            : orcamento.EstouroProjetado
                ? $"Mantido o ritmo atual, o mês deve fechar em {FormatCurrency(orcamento.ProjecaoFechamento)}."
                : orcamento.DiasRestantes > 0
                    ? $"Você ainda tem {FormatCurrency(orcamento.Restante ?? 0m)} disponíveis neste mês, algo perto de {ritmoDiario} até o fechamento."
                    : $"O mês está fechando com {FormatCurrency(orcamento.Restante ?? 0m)} de folga.";

        return FinanceMessageResult.OkHtml(BuildMessage(
            "🎯 <b>Orçamento do mês</b>",
            BuildBulletList(
                BulletField("Referência", $"{orcamento.Mes:D2}/{orcamento.Ano}"),
                BulletField("Limite", FormatCurrency(orcamento.LimiteGastos ?? 0m)),
                BulletField("Gastos", FormatCurrency(orcamento.TotalGastos)),
                BulletField("Restante", FormatCurrency(orcamento.Restante ?? 0m)),
                BulletField("Por dia até fechar", ritmoDiario),
                BulletField("Projeção", FormatCurrency(orcamento.ProjecaoFechamento)),
                BulletField($"Vs {FormatBudgetReference(referenciaAnterior.Year, referenciaAnterior.Month)}", comparacaoAnterior),
                BulletField("Sugestão equilibrada", FormatBudgetSuggestionValue(orcamento.SugestaoLimiteEquilibrado))),
            Encode(status),
            Encode(compromissoBase),
            Encode(sugestao)));
    }

    private async Task<FinanceMessageResult> ListarUltimosRegistros(CancellationToken cancellationToken)
    {
        var movimentos = await _financeOperationsService.ListarUltimosMovimentosAsync(cancellationToken: cancellationToken);
        if (!movimentos.Any())
        {
            return FinanceMessageResult.OkHtml(BuildMessage(
                "🧾 <b>Nenhum lançamento encontrado</b>",
                $"Envie algo como {Code("Café 8,50")} ou {Code("+ freelance 350")} para começar."));
        }

        var listaFormatada = string.Join(
            "\n",
            movimentos.Select((x, index) =>
                $"{index + 1}. {(x.Tipo == "Receita" ? "📈" : "📉")} <b>{Encode(FormatDescription(x.Descricao))}</b> — {Encode(FormatCurrency(x.Valor))}{FormatCategoriaHtml(x.Categoria)}{FormatPerfilHtml(x)}"));

        return FinanceMessageResult.OkHtml(BuildMessage(
            "🧾 <b>Últimos lançamentos</b>",
            listaFormatada));
    }

    private async Task<FinanceMessageResult> DesfazerUltimaAcao(CancellationToken cancellationToken)
    {
        var movimentoDesfeito = await _financeOperationsService.DesfazerUltimaAcaoAsync(cancellationToken);
        if (movimentoDesfeito is null)
        {
            return FinanceMessageResult.FalhaHtml(BuildMessage(
                "↩️ <b>Nada para desfazer</b>",
                "Ainda não há movimentações registradas para esta conta."));
        }

        return FinanceMessageResult.OkHtml(BuildMessage(
            "↩️ <b>Último lançamento desfeito</b>",
            BuildBulletList(
                BulletField("Tipo", movimentoDesfeito.Tipo),
                BulletField("Descrição", FormatDescription(movimentoDesfeito.Descricao)))));
    }

    private async Task<FinanceMessageResult> ObterStatusPlano(CancellationToken cancellationToken)
    {
        var status = await _accessPolicyService.ObterStatusAtualAsync(cancellationToken);
        var limiteLancamentos = status.LimiteLancamentosNoMesAtual is int limite
            ? $"{status.LancamentosNoMesAtual}/{limite}"
            : $"{status.LancamentosNoMesAtual} (ilimitado)";

        var linhas = new List<string>
        {
            BulletField("Plano cadastrado", status.PlanoAtual),
            BulletField("Acesso efetivo", status.PlanoEfetivo),
            BulletField("Assinatura", status.StatusAssinatura),
            BulletField("Lançamentos no mês", limiteLancamentos)
        };

        if (status.TrialAteUtc.HasValue)
        {
            linhas.Add(BulletField("Trial até", FormatDate(status.TrialAteUtc.Value)));
        }

        if (status.UpgradeSolicitadoEmUtc.HasValue)
        {
            linhas.Add(BulletField("Upgrade solicitado em", FormatDate(status.UpgradeSolicitadoEmUtc.Value)));
        }

        if (status.PremiumAteUtc.HasValue)
        {
            linhas.Add(BulletField("Premium até", FormatDate(status.PremiumAteUtc.Value)));
        }

        linhas.Add(status.PodeRegistrarLancamento
            ? BulletField("Status", "Você ainda pode registrar lançamentos.")
            : BulletField("Status", status.MotivoBloqueio ?? "Seu plano atual não permite novos lançamentos agora."));

        linhas.Add(BulletField("Resumo", status.MensagemStatus));

        if (!string.IsNullOrWhiteSpace(status.MensagemUpgrade))
        {
            linhas.Add(BulletField("Upgrade", status.MensagemUpgrade));
        }

        return FinanceMessageResult.OkHtml(BuildMessage(
            "💳 <b>Status do plano</b>",
            BuildBulletList(linhas.ToArray())));
    }

    private async Task<FinanceMessageResult> SolicitarUpgrade(CancellationToken cancellationToken)
    {
        var resultado = await _accessPolicyService.SolicitarUpgradeAsync(cancellationToken);
        var blocos = new List<string>
        {
            Encode(resultado.Mensagem)
        };

        if (resultado.SolicitadoEmUtc.HasValue)
        {
            blocos.Add(BulletField("Registrado em", FormatDate(resultado.SolicitadoEmUtc.Value)));
        }

        blocos.Insert(0, resultado.Sucesso ? "🚀 <b>Solicitação de upgrade registrada</b>" : "⚠️ <b>Não foi possível registrar o upgrade</b>");
        var mensagem = BuildMessage(blocos.ToArray());

        return resultado.Sucesso
            ? FinanceMessageResult.OkHtml(mensagem)
            : FinanceMessageResult.FalhaHtml(mensagem);
    }

    private async Task<FinanceMessageResult> ObterRelatorioMensal(CancellationToken cancellationToken)
    {
        RelatorioMensalDto relatorio;
        try
        {
            relatorio = await _financeOperationsService.ObterRelatorioMensalAsync(cancellationToken: cancellationToken);
        }
        catch (AccessPolicyException ex)
        {
            return FinanceMessageResult.FalhaHtml(BuildMessage(
                "🚫 <b>Relatório indisponível</b>",
                Encode(ex.Message),
                $"Consulte {Code("/plano")} ou envie {Code("/upgrade")} para liberar esse recurso."));
        }

        var resumoCategorias = relatorio.TopCategoriasGasto.Count == 0
            ? "• Sem gastos categorizados neste mês."
            : string.Join(
                "\n",
                relatorio.TopCategoriasGasto.Select((categoria, index) =>
                    $"{index + 1}. {Encode(categoria.Categoria)} — {Encode(FormatCurrency(categoria.TotalGasto))} ({categoria.Quantidade} lançamentos)"));

        return FinanceMessageResult.OkHtml(BuildMessage(
            "📅 <b>Relatório mensal</b>",
            BuildBulletList(
                BulletField("Referência", $"{relatorio.Mes:D2}/{relatorio.Ano}"),
                BulletField("Entradas", FormatCurrency(relatorio.TotalReceitas)),
                BulletField("Saídas", FormatCurrency(relatorio.TotalGastos)),
                BulletField("Saldo", FormatCurrency(relatorio.Saldo)),
                BulletField("Lançamentos", relatorio.TotalLancamentos.ToString(PtBr))),
            "<b>Top categorias de gasto</b>\n" + resumoCategorias));
    }

    private static FinanceMessageResult ObterMenuAjuda()
    {
        return FinanceMessageResult.OkHtml(BuildMessage(
            "🤖 <b>FinanceBot</b>",
            "Para registrar, basta enviar a mensagem no chat. Você não precisa usar comando para anotar.",
            string.Join(
                "\n",
                "<b>Como registrar gastos</b>",
                Bullet($"Use o formato {Code("descricao valor")}"),
                Bullet($"Ex.: {Code("cafe 8,50")}"),
                Bullet($"Ex.: {Code("uber 18")}"),
                Bullet($"Ex.: {Code("aluguel 1200 fixo")}")),
            string.Join(
                "\n",
                "<b>Como registrar receitas</b>",
                Bullet($"Use o formato {Code("+ descricao valor")}"),
                Bullet($"Ex.: {Code("+ salario 5000 fixo")}"),
                Bullet($"Ex.: {Code("+ freelance 350")}")),
            string.Join(
                "\n",
                "<b>Comandos úteis</b>",
                Bullet($"{Code("/resumo")} — ver saldo do dia"),
                Bullet($"{Code("/orcamento")} — acompanhar o limite do mês"),
                Bullet($"{Code("/movimentos")} — ver os últimos lançamentos"),
                Bullet($"{Code("/desfazer")} — remover o último lançamento")),
            $"Dica: use {Code("fixo")} no final quando for algo recorrente.",
            $"Se precisar de acesso ou consulta extra, você também pode usar {Code("/plano")}, {Code("/relatorio")} e {Code("/desvincular")}."));
    }

    private static FinanceMessageResult ObterMenuVinculo()
    {
        return FinanceMessageResult.OkHtml(BuildMessage(
            "🔒 <b>Como conectar este chat</b>",
            "Este Telegram ainda não está ligado à sua conta do FinanceBot.",
            BuildLinkInstructions(),
            $"Importante: troque {Code("123456")} pelo código que apareceu para você.",
            $"Depois disso, você já pode registrar por aqui com mensagens como {Code("cafe 8,50")} e {Code("+ freelance 350")}."));
    }

    private static bool TryParseLinkCommand(string comandoNormalizado, out string codigoVinculo)
    {
        codigoVinculo = string.Empty;

        if (!comandoNormalizado.StartsWith("vincular", StringComparison.Ordinal))
        {
            return false;
        }

        var partes = comandoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes.Length < 2)
        {
            return true;
        }

        codigoVinculo = partes[1];
        return true;
    }

    private static bool IsUnlinkCommand(string comandoNormalizado)
    {
        return string.Equals(comandoNormalizado, "desvincular", StringComparison.Ordinal);
    }

    private static bool IsHelpLikeCommand(string comandoNormalizado)
    {
        return comandoNormalizado is "ajuda" or "comandos" or "help" or "menu" or "start" or "oi" or "ola" or "olá" or "comecar" or "começar";
    }

    private static bool TryParseNaturalLancamento(
        string mensagem,
        out bool ehReceita,
        out string descricao,
        out decimal valor,
        out bool ehFixo)
    {
        if (TryParseNaturalPorPrefixo(mensagem, out var prefixo, out descricao, out valor, out ehFixo))
        {
            ehReceita = prefixo == "receita";
            return true;
        }

        ehReceita = false;
        descricao = string.Empty;
        valor = 0;
        ehFixo = false;
        return false;
    }

    private static bool TryParseNaturalPorPrefixo(
        string mensagem,
        out string tipo,
        out string descricao,
        out decimal valor,
        out bool ehFixo)
    {
        var normalizada = mensagem.Trim();

        if (TryStripPrefix(normalizada, ["recebi", "ganhei"], out var textoReceita) &&
            TryParseTextoNatural(textoReceita, out descricao, out valor, out ehFixo))
        {
            tipo = "receita";
            return true;
        }

        if (TryStripPrefix(normalizada, ["gastei", "paguei", "comprei"], out var textoGasto) &&
            TryParseTextoNatural(textoGasto, out descricao, out valor, out ehFixo))
        {
            tipo = "gasto";
            return true;
        }

        tipo = string.Empty;
        descricao = string.Empty;
        valor = 0;
        ehFixo = false;
        return false;
    }

    private static bool TryStripPrefix(string mensagem, string[] prefixos, out string texto)
    {
        foreach (var prefixo in prefixos)
        {
            if (mensagem.StartsWith(prefixo + " ", StringComparison.OrdinalIgnoreCase))
            {
                texto = mensagem[(prefixo.Length + 1)..].Trim();
                return true;
            }
        }

        texto = string.Empty;
        return false;
    }

    private static bool TryParseTextoNatural(string texto, out string descricao, out decimal valor, out bool ehFixo)
    {
        ehFixo = texto.EndsWith("fixo", StringComparison.OrdinalIgnoreCase);
        var conteudo = ehFixo ? texto[..^4].Trim() : texto.Trim();
        var match = NumericValueRegex.Match(conteudo);

        if (!match.Success ||
            !decimal.TryParse(match.Value.Replace(",", "."), CultureInfo.InvariantCulture, out valor))
        {
            descricao = string.Empty;
            valor = 0;
            return false;
        }

        var antes = conteudo[..match.Index].Trim();
        var depois = conteudo[(match.Index + match.Length)..].Trim();
        descricao = string.Join(' ', new[] { antes, depois }.Where(item => !string.IsNullOrWhiteSpace(item)));
        descricao = TrimLeadingConnector(descricao);

        return !string.IsNullOrWhiteSpace(descricao);
    }

    private static string TrimLeadingConnector(string descricao)
    {
        var conectores = new[] { "de ", "do ", "da ", "dos ", "das ", "no ", "na ", "nos ", "nas ", "em ", "com " };
        var resultado = descricao.Trim();

        while (true)
        {
            var conector = conectores.FirstOrDefault(item => resultado.StartsWith(item, StringComparison.OrdinalIgnoreCase));
            if (conector is null)
            {
                return resultado.Trim();
            }

            resultado = resultado[conector.Length..].Trim();
        }
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("C2", PtBr);
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToUniversalTime().ToString("dd/MM/yyyy HH:mm 'UTC'", PtBr);
    }

    private static string FormatDescription(string description)
    {
        return description
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string BuildBudgetDailyAllowanceText(OrcamentoMensalDto orcamento)
    {
        if (!orcamento.PossuiOrcamentoDefinido)
        {
            return "Defina um limite na Web";
        }

        if (orcamento.Estourado || orcamento.Restante is null || orcamento.Restante <= 0m)
        {
            return "Sem folga diária";
        }

        if (orcamento.DiasRestantes <= 0)
        {
            return "Mês no fim";
        }

        var valorPorDia = decimal.Round(
            orcamento.Restante.Value / orcamento.DiasRestantes,
            2,
            MidpointRounding.AwayFromZero);

        return $"{FormatCurrency(valorPorDia)}/dia";
    }

    private static string BuildBudgetMonthComparisonText(OrcamentoMensalDto atual, OrcamentoMensalDto anterior)
    {
        var semBaseAnterior =
            anterior.TotalGastos <= 0m &&
            anterior.TotalReceitas <= 0m &&
            !anterior.PossuiOrcamentoDefinido;

        if (semBaseAnterior)
        {
            return "Sem base anterior";
        }

        var diferenca = atual.TotalGastos - anterior.TotalGastos;
        if (diferenca == 0m)
        {
            return "Mesmo nível";
        }

        return diferenca > 0m
            ? $"{FormatCurrency(diferenca)} acima"
            : $"{FormatCurrency(Math.Abs(diferenca))} abaixo";
    }

    private static string FormatBudgetReference(int ano, int mes)
    {
        return $"{mes:D2}/{ano}";
    }

    private static string BuildBudgetMonthStatusLine(OrcamentoMensalDto orcamento)
    {
        if (!orcamento.PossuiOrcamentoDefinido)
        {
            var sugestao = orcamento.SugestaoLimiteEquilibrado.HasValue
                ? $"Sem limite definido para {FormatBudgetReference(orcamento.Ano, orcamento.Mes)}. Sua sugestão equilibrada hoje é {FormatCurrency(orcamento.SugestaoLimiteEquilibrado.Value)}."
                : $"Sem limite definido para {FormatBudgetReference(orcamento.Ano, orcamento.Mes)}.";

            return sugestao;
        }

        if (orcamento.Estourado)
        {
            return $"No mês, o orçamento já passou do limite em {FormatCurrency(Math.Abs(orcamento.Restante ?? 0m))}.";
        }

        if (orcamento.EstouroProjetado)
        {
            return $"No mês, a projeção está acima do limite e deve fechar em {FormatCurrency(orcamento.ProjecaoFechamento)}.";
        }

        if (orcamento.DiasRestantes <= 0)
        {
            return $"No mês, o fechamento indica {FormatCurrency(orcamento.Restante ?? 0m)} de folga.";
        }

        return $"No mês, ainda restam {FormatCurrency(orcamento.Restante ?? 0m)} para os próximos {orcamento.DiasRestantes} dia(s).";
    }

    private static string BuildBudgetSuggestionGuidanceText(OrcamentoMensalDto orcamento)
    {
        if (!orcamento.SugestaoLimiteEquilibrado.HasValue)
        {
            return "Ainda não há base suficiente para gerar uma sugestão confiável.";
        }

        var baseHistorica = orcamento.MesesBaseSugestao > 0
            ? $"a média dos últimos {orcamento.MesesBaseSugestao} mes(es) fechados"
            : "a projeção do mês atual";

        if (!orcamento.PossuiOrcamentoDefinido)
        {
            return $"Hoje a referência mais equilibrada é {FormatCurrency(orcamento.SugestaoLimiteEquilibrado.Value)}, usando {baseHistorica} como base.";
        }

        if (!orcamento.LimiteGastos.HasValue)
        {
            return $"A sugestão equilibrada do momento é {FormatCurrency(orcamento.SugestaoLimiteEquilibrado.Value)}.";
        }

        var diferenca = orcamento.LimiteGastos.Value - orcamento.SugestaoLimiteEquilibrado.Value;
        if (Math.Abs(diferenca) < 0.01m)
        {
            return $"Seu limite está alinhado com a sugestão equilibrada calculada a partir de {baseHistorica}.";
        }

        return diferenca > 0m
            ? $"Seu limite está {FormatCurrency(diferenca)} acima da sugestão equilibrada calculada a partir de {baseHistorica}."
            : $"Seu limite está {FormatCurrency(Math.Abs(diferenca))} abaixo da sugestão equilibrada calculada a partir de {baseHistorica}.";
    }

    private static string FormatBudgetSuggestionValue(decimal? value)
    {
        return value.HasValue ? FormatCurrency(value.Value) : "Sem base";
    }

    private static FinanceMessageResult FormatarResultadoVinculo(VinculoTelegramResult resultado)
    {
        var mensagem = BuildMessage(
            resultado.Sucesso ? "✅ <b>Chat conectado</b>" : "⚠️ <b>Não foi possível conectar este chat</b>",
            Encode(resultado.Mensagem),
            resultado.Sucesso
                ? $"A partir de agora, envie mensagens como {Code("cafe 8,50")} ou {Code("+ freelance 350")}. Para consultar, use {Code("/resumo")} ou {Code("/orcamento")}."
                : $"Confira o código gerado na Web e tente novamente com algo como {Code("/vincular 123456")}.");

        return resultado.Sucesso
            ? FinanceMessageResult.OkHtml(mensagem)
            : FinanceMessageResult.FalhaHtml(mensagem);
    }

    private static FinanceMessageResult FormatarResultadoDesvinculo(DesvinculoTelegramResult resultado)
    {
        var mensagem = BuildMessage(
            resultado.Sucesso ? "🔓 <b>Chat desvinculado</b>" : "⚠️ <b>Nenhum vínculo ativo encontrado</b>",
            Encode(resultado.Mensagem));

        return resultado.Sucesso
            ? FinanceMessageResult.OkHtml(mensagem)
            : FinanceMessageResult.FalhaHtml(mensagem);
    }

    private static string BuildMessage(params string?[] blocos)
    {
        return string.Join("\n\n", blocos.Where(bloco => !string.IsNullOrWhiteSpace(bloco)));
    }

    private static string BuildLinkInstructions()
    {
        return string.Join(
            "\n",
            "1. Entre na Web com seu login.",
            "2. Abra a área do Telegram e gere um código de vínculo.",
            $"3. Envie {Code("/vincular 123456")} neste chat.");
    }

    private static string BuildBulletList(params string[] itens)
    {
        return string.Join("\n", itens.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string Bullet(string conteudoHtml)
    {
        return $"• {conteudoHtml}";
    }

    private static string BulletField(string label, string value)
    {
        return $"• <b>{Encode(label)}:</b> {Encode(value)}";
    }

    private static string Code(string text)
    {
        return $"<code>{Encode(text)}</code>";
    }

    private static string Encode(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }

    private static string FormatCategoriaHtml(string? categoria)
    {
        return string.IsNullOrWhiteSpace(categoria)
            ? string.Empty
            : $" ({Encode(categoria)})";
    }

    private static string FormatGastoProfile(bool ehFixo, bool ehEssencial)
    {
        var labels = new List<string>();

        if (ehFixo)
        {
            labels.Add("Fixo");
        }

        if (ehEssencial)
        {
            labels.Add("Essencial");
        }

        return labels.Count > 0 ? string.Join(" • ", labels) : "Variável";
    }

    private static string FormatPerfilHtml(MovimentoDto movimento)
    {
        var labels = new List<string>();

        if (movimento.EhFixo == true)
        {
            labels.Add(movimento.Tipo == "Receita" ? "Fixa" : "Fixo");
        }

        if (movimento.EhEssencial == true)
        {
            labels.Add("Essencial");
        }

        return labels.Count > 0 ? $" — {Encode(string.Join(" • ", labels))}" : string.Empty;
    }
}
