using System.Globalization;
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
            return resultadoVinculo.Sucesso
                ? FinanceMessageResult.Ok(resultadoVinculo.Mensagem)
                : FinanceMessageResult.Falha(resultadoVinculo.Mensagem);
        }

        if (IsUnlinkCommand(comandoNormalizado))
        {
            var resultadoDesvinculo = await _identityService.DesvincularTelegramAsync(request.ChatId, cancellationToken);
            return resultadoDesvinculo.Sucesso
                ? FinanceMessageResult.Ok(resultadoDesvinculo.Mensagem)
                : FinanceMessageResult.Falha(resultadoDesvinculo.Mensagem);
        }

        if (_currentUserContext.UsuarioId is null)
        {
            return IsHelpLikeCommand(comandoNormalizado)
                ? ObterMenuVinculo()
                : FinanceMessageResult.Falha(
                    "🔒 Este chat ainda não está vinculado.\n" +
                    "Gere o codigo na Web/API e envie aqui:\n" +
                    "• /vincular 123456\n\n" +
                    "Se quiser, mande ajuda que eu te mostro o passo a passo.");
        }

        return comandoNormalizado switch
        {
            "total" or "resumo" => await ObterResumoFinanceiro(cancellationToken),
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
        var (valido, desc, valor, _) = ParseDados(mensagem);
        if (!valido)
        {
            return FinanceMessageResult.Falha(
                "Nao consegui entender esse gasto.\n" +
                "Tente um destes formatos:\n" +
                "• Gasto: Café 8,50\n" +
                "• Gasto: Uber 15\n" +
                "• Natural: gastei 18 no uber\n" +
                "• Receita: + Freelance 350");
        }

        GastoDto gasto;
        try
        {
            gasto = await _financeOperationsService.RegistrarGastoAsync(
                new CriarGastoRequest(desc, valor),
                cancellationToken);
        }
        catch (AccessPolicyException ex)
        {
            return FinanceMessageResult.Falha($"🚫 {ex.Message}");
        }

        return FinanceMessageResult.Ok(
            "✅ Gasto registrado\n" +
            $"• Descrição: {FormatDescription(gasto.Descricao)}\n" +
            $"• Valor: {FormatCurrency(gasto.Valor)}\n" +
            $"• Categoria: {gasto.Categoria}\n" +
            "• Proximo passo: envie /resumo ou /movimentos se quiser conferir.");
    }

    private async Task<FinanceMessageResult> RegistrarReceita(string mensagem, CancellationToken cancellationToken)
    {
        string limpa = mensagem.Replace("+", "").Replace("ganho", "", StringComparison.OrdinalIgnoreCase).Trim();

        var (valido, desc, valor, ehFixo) = ParseDados(limpa);
        if (!valido)
        {
            return FinanceMessageResult.Falha(
                "Nao consegui entender essa receita.\n" +
                "Tente um destes formatos:\n" +
                "• Receita: + Freelance 350\n" +
                "• Receita: receita Venda 120\n" +
                "• Receita fixa: + Salário 5000 fixo\n" +
                "• Natural: recebi 350 do freelance");
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
            return FinanceMessageResult.Falha($"🚫 {ex.Message}");
        }

        return FinanceMessageResult.Ok(
            "✅ Receita registrada\n" +
            $"• Descrição: {FormatDescription(receita.Descricao)}\n" +
            $"• Valor: {FormatCurrency(receita.Valor)}\n" +
            $"• Tipo: {(receita.EhFixo ? "Fixa" : "Variável")}\n" +
            "• Proximo passo: envie /resumo ou /movimentos se quiser conferir.");
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
            var menu = ObterMenuAjuda();
            return FinanceMessageResult.Falha(
                "Nao entendi essa mensagem.\n" +
                "Se quiser, me mande algo como 'gastei 18 no uber' ou '+ freelance 350'.\n\n" +
                menu.Mensagem);
        }

        return resultado;
    }

    private async Task<FinanceMessageResult> ObterResumoFinanceiro(CancellationToken cancellationToken)
    {
        var resumo = await _financeOperationsService.ObterResumoAsync(cancellationToken: cancellationToken);

        return FinanceMessageResult.Ok(
            "📊 Resumo de hoje\n" +
            $"• Entradas: {FormatCurrency(resumo.Ganhos)}\n" +
            $"• Saídas: {FormatCurrency(resumo.Gastos)}\n" +
            $"• Saldo: {FormatCurrency(resumo.Saldo)}");
    }

    private async Task<FinanceMessageResult> ListarUltimosRegistros(CancellationToken cancellationToken)
    {
        var movimentos = await _financeOperationsService.ListarUltimosMovimentosAsync(cancellationToken: cancellationToken);
        if (!movimentos.Any())
        {
            return FinanceMessageResult.Ok("📝 Ainda nao ha movimentacoes registradas.\nEnvie algo como 'Cafe 8,50' ou '+ freelance 350' para comecar.");
        }

        var listaFormatada = string.Join(
            "\n",
            movimentos.Select((x, index) =>
                $"{index + 1}. {(x.Tipo == "Receita" ? "📈" : "📉")} {FormatDescription(x.Descricao)} — {FormatCurrency(x.Valor)}{FormatCategoria(x.Categoria)}"));

        return FinanceMessageResult.Ok($"📝 Últimos movimentos\n\n{listaFormatada}");
    }

    private async Task<FinanceMessageResult> DesfazerUltimaAcao(CancellationToken cancellationToken)
    {
        var movimentoDesfeito = await _financeOperationsService.DesfazerUltimaAcaoAsync(cancellationToken);
        if (movimentoDesfeito is null)
        {
            return FinanceMessageResult.Falha("↩️ Ainda não há nenhuma movimentação para desfazer.");
        }

        return FinanceMessageResult.Ok(
            "↩️ Último movimento desfeito\n" +
            $"• Tipo: {movimentoDesfeito.Tipo}\n" +
            $"• Descrição: {FormatDescription(movimentoDesfeito.Descricao)}");
    }

    private async Task<FinanceMessageResult> ObterStatusPlano(CancellationToken cancellationToken)
    {
        var status = await _accessPolicyService.ObterStatusAtualAsync(cancellationToken);
        var limiteLancamentos = status.LimiteLancamentosNoMesAtual is int limite
            ? $"{status.LancamentosNoMesAtual}/{limite}"
            : $"{status.LancamentosNoMesAtual} (ilimitado)";

        var mensagem =
            "💳 Seu plano\n" +
            $"• Plano cadastrado: {status.PlanoAtual}\n" +
            $"• Acesso efetivo: {status.PlanoEfetivo}\n" +
            $"• Assinatura: {status.StatusAssinatura}\n" +
            $"• Lançamentos no mês: {limiteLancamentos}";

        if (status.TrialAteUtc.HasValue)
        {
            mensagem += $"\n• Trial até: {FormatDate(status.TrialAteUtc.Value)}";
        }

        if (status.UpgradeSolicitadoEmUtc.HasValue)
        {
            mensagem += $"\n• Upgrade solicitado em: {FormatDate(status.UpgradeSolicitadoEmUtc.Value)}";
        }

        if (status.PremiumAteUtc.HasValue)
        {
            mensagem += $"\n• Premium até: {FormatDate(status.PremiumAteUtc.Value)}";
        }

        mensagem += status.PodeRegistrarLancamento
            ? "\n• Status: você ainda pode registrar lançamentos."
            : $"\n• Status: {status.MotivoBloqueio}";

        mensagem += $"\n• Resumo: {status.MensagemStatus}";

        if (!string.IsNullOrWhiteSpace(status.MensagemUpgrade))
        {
            mensagem += $"\n• Upgrade: {status.MensagemUpgrade}";
        }

        return FinanceMessageResult.Ok(mensagem);
    }

    private async Task<FinanceMessageResult> SolicitarUpgrade(CancellationToken cancellationToken)
    {
        var resultado = await _accessPolicyService.SolicitarUpgradeAsync(cancellationToken);
        var mensagem = $"🚀 {resultado.Mensagem}";

        if (resultado.SolicitadoEmUtc.HasValue)
        {
            mensagem += $"\n• Registrado em: {FormatDate(resultado.SolicitadoEmUtc.Value)}";
        }

        return resultado.Sucesso
            ? FinanceMessageResult.Ok(mensagem)
            : FinanceMessageResult.Falha(mensagem);
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
            return FinanceMessageResult.Falha($"🚫 {ex.Message}");
        }

        var resumoCategorias = relatorio.TopCategoriasGasto.Count == 0
            ? "• Sem gastos categorizados neste mês."
            : string.Join(
                "\n",
                relatorio.TopCategoriasGasto.Select((categoria, index) =>
                    $"{index + 1}. {categoria.Categoria} — {FormatCurrency(categoria.TotalGasto)} ({categoria.Quantidade} lançamentos)"));

        return FinanceMessageResult.Ok(
            "📅 Relatório mensal\n" +
            $"• Referência: {relatorio.Mes:D2}/{relatorio.Ano}\n" +
            $"• Entradas: {FormatCurrency(relatorio.TotalReceitas)}\n" +
            $"• Saídas: {FormatCurrency(relatorio.TotalGastos)}\n" +
            $"• Saldo: {FormatCurrency(relatorio.Saldo)}\n" +
            $"• Lançamentos: {relatorio.TotalLancamentos}\n\n" +
            "Categorias do mês:\n" +
            resumoCategorias);
    }

    private static FinanceMessageResult ObterMenuAjuda()
    {
        return FinanceMessageResult.Ok(
            "🤖 FinanceBot\n\n" +
            "Voce pode registrar de forma direta, sem decorar muita coisa:\n" +
            "• Gasto: Café 8,50\n" +
            "• Gasto: Uber 15\n" +
            "• Natural: gastei 18 no uber\n" +
            "• Receita: + Freelance 350\n" +
            "• Natural: recebi 350 do freelance\n" +
            "• Receita fixa: + Salário 5000 fixo\n\n" +
            "Comandos disponíveis:\n" +
            "• ajuda / menu / oi — mostra este menu\n" +
            "• total / resumo — mostra o resumo do dia\n" +
            "• listar / movimentos — mostra os últimos movimentos\n" +
            "• relatorio — mostra o relatório mensal (Premium/trial)\n" +
            "• plano / status — mostra seu plano e sua quota atual\n" +
            "• upgrade / assinar — registra seu pedido de upgrade para o Premium\n" +
            "• desfazer — remove o último lançamento\n" +
            "• desvincular — remove o vínculo deste chat\n\n" +
            "Se preferir, os comandos também funcionam com / no início.");
    }

    private static FinanceMessageResult ObterMenuVinculo()
    {
        return FinanceMessageResult.Ok(
            "🤖 FinanceBot\n\n" +
            "Este chat ainda nao esta vinculado a sua conta.\n" +
            "1. Faca login na Web/API.\n" +
            "2. Gere um codigo de vinculo.\n" +
            "3. Envie aqui:\n" +
            "• /vincular 123456\n\n" +
            "Depois disso eu ja consigo registrar seus lancamentos por aqui.");
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

    private static string FormatCategoria(string? categoria)
    {
        return string.IsNullOrWhiteSpace(categoria)
            ? string.Empty
            : $" ({categoria})";
    }
}
