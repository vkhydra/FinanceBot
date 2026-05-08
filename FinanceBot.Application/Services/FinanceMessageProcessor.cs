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
                    "🔒 <b>Chat não vinculado</b>",
                    "Para registrar lançamentos por aqui, primeiro conecte este chat à sua conta.",
                    string.Join(
                        "\n",
                        "1. Acesse a Web ou a API.",
                        "2. Gere um código de vínculo.",
                        $"3. Envie {Code("/vincular 123456")} neste chat."),
                    $"Se precisar, envie {Code("ajuda")} e eu mostro o passo a passo."));
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
            return FinanceMessageResult.FalhaHtml(BuildMessage(
                "⚠️ <b>Não consegui interpretar esse gasto</b>",
                "Use um destes formatos:",
                BuildBulletList(
                    Bullet(Code("Café 8,50")),
                    Bullet(Code("Uber 15")),
                    Bullet(Code("gastei 18 no uber")))));
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
                BulletField("Categoria", gasto.Categoria)),
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

        return FinanceMessageResult.OkHtml(BuildMessage(
            "📊 <b>Resumo do dia</b>",
            BuildBulletList(
                BulletField("Entradas", FormatCurrency(resumo.Ganhos)),
                BulletField("Saídas", FormatCurrency(resumo.Gastos)),
                BulletField("Saldo", FormatCurrency(resumo.Saldo)))));
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
                $"{index + 1}. {(x.Tipo == "Receita" ? "📈" : "📉")} <b>{Encode(FormatDescription(x.Descricao))}</b> — {Encode(FormatCurrency(x.Valor))}{FormatCategoriaHtml(x.Categoria)}"));

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
            "Envie uma mensagem curta e eu registro o lançamento para você.",
            string.Join(
                "\n",
                "<b>Exemplos rápidos</b>",
                Bullet(Code("Café 8,50")),
                Bullet(Code("gastei 18 no uber")),
                Bullet(Code("+ freelance 350")),
                Bullet(Code("recebi 350 do freelance")),
                Bullet(Code("+ salário 5000 fixo"))),
            string.Join(
                "\n",
                "<b>Comandos</b>",
                Bullet($"{Code("ajuda")} ou {Code("menu")} — mostra este menu"),
                Bullet($"{Code("resumo")} ou {Code("total")} — mostra o fechamento do dia"),
                Bullet($"{Code("movimentos")} ou {Code("listar")} — mostra os últimos lançamentos"),
                Bullet($"{Code("relatorio")} — mostra o relatório mensal (Premium ou trial)"),
                Bullet($"{Code("plano")} ou {Code("status")} — mostra seu plano e sua quota atual"),
                Bullet($"{Code("upgrade")} ou {Code("assinar")} — registra seu interesse no Premium"),
                Bullet($"{Code("desfazer")} — remove o último lançamento"),
                Bullet($"{Code("desvincular")} — desconecta este chat da conta")),
            $"Você também pode usar os comandos com {Code("/")} no início."));
    }

    private static FinanceMessageResult ObterMenuVinculo()
    {
        return FinanceMessageResult.OkHtml(BuildMessage(
            "🤖 <b>FinanceBot</b>",
            "Este chat ainda não está conectado à sua conta.",
            string.Join(
                "\n",
                "1. Faça login na Web ou na API.",
                "2. Gere um código de vínculo.",
                $"3. Envie {Code("/vincular 123456")} aqui."),
            "Depois disso, você poderá registrar gastos e receitas diretamente no Telegram."));
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

    private static FinanceMessageResult FormatarResultadoVinculo(VinculoTelegramResult resultado)
    {
        var mensagem = BuildMessage(
            resultado.Sucesso ? "✅ <b>Chat vinculado</b>" : "⚠️ <b>Não foi possível concluir o vínculo</b>",
            Encode(resultado.Mensagem),
            resultado.Sucesso
                ? $"Agora você já pode enviar {Code("gastei 18 no uber")} ou consultar {Code("/resumo")}."
                : null);

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
}
