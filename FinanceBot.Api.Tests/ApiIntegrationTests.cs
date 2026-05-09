using System.Net;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceBot.Api.Tests.Infrastructure;
using FinanceBot.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinanceBot.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ApiIntegrationTests
{
    private readonly PostgresContainerFixture _postgres;

    public ApiIntegrationTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Register_login_and_billing_status_start_with_trial()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync();
        var billing = await app.GetBillingStatusAsync(auth.Token);

        Assert.Equal(auth.Email, auth.Email);
        Assert.Equal("Free", billing.PlanoAtual);
        Assert.Equal("Premium", billing.PlanoEfetivo);
        Assert.True(billing.TrialAtivo);
        Assert.Equal(7, billing.DiasRestantesTrial);
        Assert.Contains("trial Premium", billing.MensagemStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Monthly_report_is_available_during_trial_and_summarizes_categories()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("report");
        await app.CreateReceitaAsync(auth.Token, "Salario", 5000m, true);
        await app.CreateGastoAsync(auth.Token, "Uber centro", 25m);
        await app.CreateGastoAsync(auth.Token, "Mercado bairro", 80m);

        var report = await app.GetMonthlyReportAsync(auth.Token);

        Assert.Equal(DateTime.UtcNow.Year, report.Ano);
        Assert.Equal(DateTime.UtcNow.Month, report.Mes);
        Assert.Equal(5000m, report.TotalReceitas);
        Assert.Equal(105m, report.TotalGastos);
        Assert.Equal(4895m, report.Saldo);
        Assert.Contains(report.TopCategoriasGasto, item => item.Categoria == "Mercado");
        Assert.Contains(report.TopCategoriasGasto, item => item.Categoria == "Transporte");
    }

    [Fact]
    public async Task Monthly_report_is_blocked_for_free_users_without_trial()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("free-report");
        await app.DisableTrialAsync(auth.UsuarioId);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/relatorios/mensal");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using var response = await app.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Premium", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upgrade_request_sets_pending_status_and_is_idempotent()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("upgrade");
        await app.DisableTrialAsync(auth.UsuarioId);

        var firstRequest = await app.RequestUpgradeAsync(auth.Token);
        var billing = await app.GetBillingStatusAsync(auth.Token);
        var secondRequest = await app.RequestUpgradeAsync(auth.Token);

        Assert.True(firstRequest.Sucesso);
        Assert.True(firstRequest.UpgradePendente);
        Assert.NotNull(firstRequest.SolicitadoEmUtc);
        Assert.Equal("Free", billing.PlanoAtual);
        Assert.Equal("Free", billing.PlanoEfetivo);
        Assert.Equal("Pendente", billing.StatusAssinatura);
        Assert.True(billing.UpgradePendente);
        Assert.False(billing.PodeSolicitarUpgrade);
        Assert.Equal(firstRequest.SolicitadoEmUtc, billing.UpgradeSolicitadoEmUtc);
        Assert.Contains("solicitado", billing.MensagemStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("já foi registrado", billing.MensagemUpgrade, StringComparison.OrdinalIgnoreCase);
        Assert.True(secondRequest.Sucesso);
        Assert.True(secondRequest.UpgradePendente);
        Assert.Equal(firstRequest.SolicitadoEmUtc, secondRequest.SolicitadoEmUtc);
    }

    [Fact]
    public async Task Generating_new_link_code_invalidates_previous_code()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("link-rotation");
        var firstCode = await app.GenerateLinkCodeAsync(auth.Token);
        var secondCode = await app.GenerateLinkCodeAsync(auth.Token);

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        var outdatedAttempt = await identityService.VincularTelegramAsync(123456, firstCode.Codigo);
        var validAttempt = await identityService.VincularTelegramAsync(123456, secondCode.Codigo);

        Assert.False(outdatedAttempt.Sucesso);
        Assert.Contains("inválido ou expirado", outdatedAttempt.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.True(validAttempt.Sucesso);
    }

    [Fact]
    public async Task Telegram_link_flow_binds_chat_to_registered_user()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync();
        var code = await app.GenerateLinkCodeAsync(auth.Token);

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var result = await identityService.VincularTelegramAsync(998877, code.Codigo);
        var linkedUser = await identityService.ObterUsuarioPorTelegramAsync(998877);

        Assert.True(result.Sucesso);
        Assert.Contains("vinculado", result.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(linkedUser);
        Assert.Equal(auth.UsuarioId, linkedUser!.UsuarioId);
    }

    [Fact]
    public async Task Telegram_help_menu_uses_html_formatting_for_professional_copy()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("telegram-help");

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserContextAccessor>();
        currentUser.SetTelegramChatId(998877);
        currentUser.SetAuthenticatedUser(auth.UsuarioId, auth.Email);

        var processor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();
        var result = await processor.ProcessarMensagemAsync(new FinanceMessageRequest("ajuda", 998877));

        Assert.True(result.Sucesso);
        Assert.True(result.UsaHtml);
        Assert.Contains("<b>FinanceBot</b>", result.Mensagem, StringComparison.Ordinal);
        Assert.Contains("Como registrar gastos", result.Mensagem, StringComparison.Ordinal);
        Assert.Contains("<code>cafe 8,50</code>", result.Mensagem, StringComparison.Ordinal);
        Assert.Contains("<code>/resumo</code>", result.Mensagem, StringComparison.Ordinal);
        Assert.Contains("<code>/plano</code>", result.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Telegram_help_menu_for_unlinked_chat_explains_link_step_by_step()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserContextAccessor>();
        currentUser.SetTelegramChatId(998877);

        var processor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();
        var result = await processor.ProcessarMensagemAsync(new FinanceMessageRequest("ajuda", 998877));

        Assert.True(result.Sucesso);
        Assert.Contains("Como conectar este chat", result.Mensagem, StringComparison.Ordinal);
        Assert.Contains("gere um código de vínculo", result.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<code>/vincular 123456</code>", result.Mensagem, StringComparison.Ordinal);
        Assert.Contains("<code>cafe 8,50</code>", result.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Telegram_link_command_returns_clear_next_step_for_customer()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("telegram-link-copy");
        var code = await app.GenerateLinkCodeAsync(auth.Token);

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();
        var result = await processor.ProcessarMensagemAsync(new FinanceMessageRequest($"/vincular {code.Codigo}", 998877));
        var linkedUser = await scope.ServiceProvider.GetRequiredService<IIdentityService>().ObterUsuarioPorTelegramAsync(998877);

        Assert.True(result.Sucesso);
        Assert.True(result.UsaHtml);
        Assert.Contains("Chat conectado", result.Mensagem, StringComparison.Ordinal);
        Assert.Contains("<code>cafe 8,50</code>", result.Mensagem, StringComparison.Ordinal);
        Assert.Contains("<code>/orcamento</code>", result.Mensagem, StringComparison.Ordinal);
        Assert.NotNull(linkedUser);
        Assert.Equal(auth.UsuarioId, linkedUser!.UsuarioId);
    }

    [Fact]
    public async Task Unlink_endpoint_clears_binding_and_is_idempotent()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("unlink");
        var code = await app.GenerateLinkCodeAsync(auth.Token);

        await using (var scope = app.Factory.Services.CreateAsyncScope())
        {
            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            var linkResult = await identityService.VincularTelegramAsync(998877, code.Codigo);
            Assert.True(linkResult.Sucesso);
        }

        var firstResponse = await app.UnlinkAsync(auth.Token);

        Assert.True(firstResponse.Sucesso);
        Assert.Contains("removido com sucesso", firstResponse.Mensagem, StringComparison.OrdinalIgnoreCase);

        await using (var scope = app.Factory.Services.CreateAsyncScope())
        {
            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            var linkedUser = await identityService.ObterUsuarioPorTelegramAsync(998877);
            Assert.Null(linkedUser);
        }

        var secondResponse = await app.UnlinkAsync(auth.Token);

        Assert.True(secondResponse.Sucesso);
        Assert.Contains("já está sem vínculo", secondResponse.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Protected_endpoints_return_only_authenticated_users_data()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var firstUser = await app.RegisterAsync("alice");
        var secondUser = await app.RegisterAsync("bob");

        await app.CreateGastoAsync(firstUser.Token, "Mercado Alice", 10m);
        await app.CreateGastoAsync(secondUser.Token, "Mercado Bob", 20m);

        var movimentos = await app.GetMovimentosAsync(firstUser.Token);

        Assert.Single(movimentos);
        Assert.Equal("Mercado Alice", movimentos[0].Descricao);
    }

    [Fact]
    public async Task Unified_movements_listing_supports_filters_for_type_text_and_category()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("listing");
        await app.CreateReceitaAsync(auth.Token, "Salario", 5000m, true);
        await app.CreateGastoAsync(auth.Token, "Mercado Central", 100m);
        await app.CreateGastoAsync(auth.Token, "Uber Casa", 25m);

        var mercadoMovimentos = await app.ListMovimentosAsync(
            auth.Token,
            "/api/movimentos?tipo=Gasto&categoria=Mercado&busca=Mercado&limite=20");

        Assert.Single(mercadoMovimentos);
        Assert.Equal("Gasto", mercadoMovimentos[0].Tipo);
        Assert.Equal("Mercado Central", mercadoMovimentos[0].Descricao);
        Assert.Equal("Mercado", mercadoMovimentos[0].Categoria);
    }

    [Fact]
    public async Task Specific_gasto_and_receita_can_be_updated_and_deleted()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("record-crud");
        var gasto = await app.CreateGastoAsync(auth.Token, "Mercado bairro", 80m, "Compra do sabado");
        var receita = await app.CreateReceitaAsync(auth.Token, "Freelance", 500m, false, "Projeto site");

        var updatedGasto = await app.UpdateGastoAsync(
            auth.Token,
            gasto.Id,
            "Mercado atacado",
            95m,
            new DateOnly(2026, 05, 01),
            "Compras",
            "Reposicao da despensa",
            true,
            true);
        var updatedReceita = await app.UpdateReceitaAsync(
            auth.Token,
            receita.Id,
            "Freelance maio",
            650m,
            new DateOnly(2026, 05, 02),
            true,
            "Projeto fechado");

        Assert.Equal("Mercado atacado", updatedGasto.Descricao);
        Assert.Equal(95m, updatedGasto.Valor);
        Assert.Equal("Compras", updatedGasto.Categoria);
        Assert.Equal("Reposicao da despensa", updatedGasto.Observacao);
        Assert.True(updatedGasto.EhFixo);
        Assert.True(updatedGasto.EhEssencial);
        Assert.Equal("Web", updatedGasto.Origem);
        Assert.Equal(new DateOnly(2026, 05, 01), DateOnly.FromDateTime(updatedGasto.Data));
        Assert.Equal("Freelance maio", updatedReceita.Descricao);
        Assert.Equal(650m, updatedReceita.Valor);
        Assert.True(updatedReceita.EhFixo);
        Assert.Equal("Projeto fechado", updatedReceita.Observacao);
        Assert.Equal("Web", updatedReceita.Origem);
        Assert.Equal(new DateOnly(2026, 05, 02), DateOnly.FromDateTime(updatedReceita.Data));

        await app.DeleteGastoAsync(auth.Token, gasto.Id);
        await app.DeleteReceitaAsync(auth.Token, receita.Id);

        var movimentos = await app.ListMovimentosAsync(auth.Token, "/api/movimentos?limite=20");

        Assert.DoesNotContain(movimentos, item => item.Id == gasto.Id);
        Assert.DoesNotContain(movimentos, item => item.Id == receita.Id);
    }

    [Fact]
    public async Task Movements_expose_origin_and_observation_and_support_origin_filter()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("record-origin");
        var gastoWeb = await app.CreateGastoAsync(auth.Token, "Mercado premium", 120m, "Compra mensal");

        await using (var scope = app.Factory.Services.CreateAsyncScope())
        {
            var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserContextAccessor>();
            currentUser.SetTelegramChatId(998877);
            currentUser.SetAuthenticatedUser(auth.UsuarioId, auth.Email);

            var processor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();
            var result = await processor.ProcessarMensagemAsync(new FinanceMessageRequest("gastei 18 no uber", 998877));
            Assert.True(result.Sucesso);
        }

        var todos = await app.ListMovimentosAsync(auth.Token, "/api/movimentos?limite=20");
        var telegramOnly = await app.ListMovimentosAsync(auth.Token, "/api/movimentos?origem=Telegram&limite=20");

        Assert.Contains(todos, item => item.Id == gastoWeb.Id && item.Origem == "Web" && item.Observacao == "Compra mensal");
        Assert.Contains(telegramOnly, item => item.Origem == "Telegram" && item.Descricao == "uber" && item.EhEssencial == true);
        Assert.DoesNotContain(telegramOnly, item => item.Origem == "Web");
    }

    [Fact]
    public async Task Telegram_messages_escape_user_content_when_using_html_formatting()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("telegram-html");

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserContextAccessor>();
        currentUser.SetTelegramChatId(998877);
        currentUser.SetAuthenticatedUser(auth.UsuarioId, auth.Email);

        var processor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();
        var result = await processor.ProcessarMensagemAsync(new FinanceMessageRequest("Mercado <vip> 18", 998877));

        Assert.True(result.Sucesso);
        Assert.True(result.UsaHtml);
        Assert.Contains("Mercado &lt;vip&gt;", result.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("<vip>", result.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Telegram_gasto_supports_fixed_suffix_and_exposes_financial_signals()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("telegram-fixed");

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserContextAccessor>();
        currentUser.SetTelegramChatId(998877);
        currentUser.SetAuthenticatedUser(auth.UsuarioId, auth.Email);

        var processor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();
        var result = await processor.ProcessarMensagemAsync(new FinanceMessageRequest("internet 120 fixo", 998877));

        Assert.True(result.Sucesso);
        Assert.Contains("Perfil", result.Mensagem, StringComparison.Ordinal);

        var movimentos = await app.ListMovimentosAsync(auth.Token, "/api/movimentos?limite=20");
        Assert.Contains(movimentos, item =>
            item.Tipo == "Gasto" &&
            item.Descricao == "internet" &&
            item.Categoria == "Moradia" &&
            item.EhFixo == true &&
            item.EhEssencial == true);
    }

    [Fact]
    public async Task Free_plan_blocks_new_launches_after_monthly_limit_when_trial_is_disabled()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("quota");
        await app.DisableTrialAsync(auth.UsuarioId);
        var initialBilling = await app.GetBillingStatusAsync(auth.Token);
        var freeLimit = initialBilling.LimiteLancamentosNoMesAtual ?? throw new InvalidOperationException("Expected a Free plan monthly limit in tests.");

        for (var index = 0; index < freeLimit - 1; index++)
        {
            await app.CreateGastoAsync(auth.Token, $"Cafe {index}", 5m);
        }

        await app.CreateReceitaAsync(auth.Token, "Freelance extra", 100m, false);

        var billing = await app.GetBillingStatusAsync(auth.Token);

        Assert.Equal("Free", billing.PlanoEfetivo);
        Assert.Equal(freeLimit, billing.LancamentosNoMesAtual);
        Assert.Equal(freeLimit, billing.LimiteLancamentosNoMesAtual);
        Assert.False(billing.PodeRegistrarLancamento);
        Assert.Contains("atingiu o limite", billing.MensagemStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upgrade", billing.MensagemUpgrade, StringComparison.OrdinalIgnoreCase);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/gastos")
        {
            Content = JsonContent.Create(new { Descricao = "Bloqueado", Valor = 10m })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using var response = await app.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("upgrade", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gasto_creation_uses_accent_insensitive_categorization_and_exposes_category()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("categorization");
        var gasto = await app.CreateGastoAsync(auth.Token, "CAFÉ da manhã", 12.5m);

        var gastos = await app.GetGastosAsync(auth.Token);
        var movimentos = await app.GetMovimentosAsync(auth.Token);

        Assert.Equal("Alimentacao", gasto.Categoria);
        Assert.False(gasto.EhFixo);
        Assert.False(gasto.EhEssencial);
        Assert.Contains(gastos, item => item.Descricao == "CAFÉ da manhã" && item.Categoria == "Alimentacao");
        Assert.Contains(movimentos, item =>
            item.Descricao == "CAFÉ da manhã" &&
            item.Categoria == "Alimentacao" &&
            item.EhFixo == false &&
            item.EhEssencial == false);
    }

    [Fact]
    public async Task Gasto_creation_marks_housing_expenses_as_fixed_and_essential()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("gasto-profile");
        var gasto = await app.CreateGastoAsync(auth.Token, "Aluguel centro", 1450m);

        Assert.Equal("Moradia", gasto.Categoria);
        Assert.True(gasto.EhFixo);
        Assert.True(gasto.EhEssencial);
    }

    [Fact]
    public async Task Monthly_budget_can_be_saved_and_summarizes_current_month_spending()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("budget");
        await app.CreateReceitaAsync(auth.Token, "Salario", 4000m, true);
        await app.CreateGastoAsync(auth.Token, "Aluguel centro", 1200m);
        await app.CreateGastoAsync(auth.Token, "Cinema centro", 50m);

        var budget = await app.UpdateMonthlyBudgetAsync(auth.Token, 2000m);

        Assert.Equal(DateTime.UtcNow.Year, budget.Ano);
        Assert.Equal(DateTime.UtcNow.Month, budget.Mes);
        Assert.True(budget.PossuiOrcamentoDefinido);
        Assert.Equal(2000m, budget.LimiteGastos);
        Assert.Equal(1250m, budget.TotalGastos);
        Assert.Equal(4000m, budget.TotalReceitas);
        Assert.Equal(1200m, budget.GastoFixo);
        Assert.Equal(1200m, budget.GastoEssencial);
        Assert.Equal(50m, budget.GastoNaoEssencial);
        Assert.Equal(750m, budget.Restante);
        Assert.False(budget.Estourado);
    }

    [Fact]
    public async Task Monthly_budget_is_isolated_per_user()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var firstUser = await app.RegisterAsync("budget-alice");
        var secondUser = await app.RegisterAsync("budget-bob");

        await app.UpdateMonthlyBudgetAsync(firstUser.Token, 1800m);
        await app.UpdateMonthlyBudgetAsync(secondUser.Token, 900m);

        var firstBudget = await app.GetMonthlyBudgetAsync(firstUser.Token);
        var secondBudget = await app.GetMonthlyBudgetAsync(secondUser.Token);

        Assert.Equal(1800m, firstBudget.LimiteGastos);
        Assert.Equal(900m, secondBudget.LimiteGastos);
    }

    [Fact]
    public async Task Monthly_budget_can_be_queried_for_a_specific_month()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("budget-history");
        var previousReference = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);

        var previousExpense = await app.CreateGastoAsync(auth.Token, "Mercado anterior", 150m);
        await app.UpdateGastoAsync(
            auth.Token,
            previousExpense.Id,
            previousExpense.Descricao,
            previousExpense.Valor,
            previousReference,
            previousExpense.Categoria,
            previousExpense.Observacao,
            previousExpense.EhFixo,
            previousExpense.EhEssencial);

        await app.UpdateMonthlyBudgetAsync(auth.Token, 700m, previousReference.Year, previousReference.Month);
        await app.UpdateMonthlyBudgetAsync(auth.Token, 1200m);

        var previousBudget = await app.GetMonthlyBudgetAsync(auth.Token, previousReference.Year, previousReference.Month);

        Assert.Equal(previousReference.Year, previousBudget.Ano);
        Assert.Equal(previousReference.Month, previousBudget.Mes);
        Assert.Equal(700m, previousBudget.LimiteGastos);
        Assert.Equal(150m, previousBudget.TotalGastos);
    }

    [Fact]
    public async Task Monthly_budget_returns_shared_limit_suggestions_from_previous_months()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("budget-suggestions");
        var previousReference = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);

        await app.CreateReceitaAsync(auth.Token, "Salario atual", 4500m, true);
        var previousIncome = await app.CreateReceitaAsync(auth.Token, "Salario anterior", 4000m, true);
        var previousRent = await app.CreateGastoAsync(auth.Token, "Aluguel centro", 600m);
        var previousLeisure = await app.CreateGastoAsync(auth.Token, "Cinema bairro", 200m);

        await app.UpdateReceitaAsync(
            auth.Token,
            previousIncome.Id,
            previousIncome.Descricao,
            previousIncome.Valor,
            previousReference,
            previousIncome.EhFixo,
            previousIncome.Observacao);
        await app.UpdateGastoAsync(
            auth.Token,
            previousRent.Id,
            previousRent.Descricao,
            previousRent.Valor,
            previousReference,
            previousRent.Categoria,
            previousRent.Observacao,
            previousRent.EhFixo,
            previousRent.EhEssencial);
        await app.UpdateGastoAsync(
            auth.Token,
            previousLeisure.Id,
            previousLeisure.Descricao,
            previousLeisure.Valor,
            previousReference,
            previousLeisure.Categoria,
            previousLeisure.Observacao,
            previousLeisure.EhFixo,
            previousLeisure.EhEssencial);

        var budget = await app.GetMonthlyBudgetAsync(auth.Token);

        Assert.Equal(1, budget.MesesBaseSugestao);
        Assert.Equal(630m, budget.SugestaoLimiteSeguro);
        Assert.Equal(700m, budget.SugestaoLimiteEquilibrado);
        Assert.Equal(760m, budget.SugestaoLimiteFlexivel);
    }

    [Fact]
    public async Task Telegram_budget_command_shows_daily_pace_and_previous_month_comparison()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("telegram-budget");
        var previousReference = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
        var previousLabel = $"{previousReference.Month:D2}/{previousReference.Year}";
        var previousExpense = await app.CreateGastoAsync(auth.Token, "Mercado anterior", 120m);
        await app.UpdateGastoAsync(
            auth.Token,
            previousExpense.Id,
            previousExpense.Descricao,
            previousExpense.Valor,
            previousReference,
            previousExpense.Categoria,
            previousExpense.Observacao,
            previousExpense.EhFixo,
            previousExpense.EhEssencial);
        await app.UpdateMonthlyBudgetAsync(auth.Token, 700m, previousReference.Year, previousReference.Month);
        await app.CreateGastoAsync(auth.Token, "Mercado bairro", 200m);
        await app.UpdateMonthlyBudgetAsync(auth.Token, 800m);

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserContextAccessor>();
        currentUser.SetTelegramChatId(998877);
        currentUser.SetAuthenticatedUser(auth.UsuarioId, auth.Email);

        var processor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();
        var result = await processor.ProcessarMensagemAsync(new FinanceMessageRequest("orcamento", 998877));
        var message = WebUtility.HtmlDecode(result.Mensagem);

        Assert.True(result.Sucesso);
        Assert.Contains("Orçamento do mês", message, StringComparison.Ordinal);
        Assert.Contains("Restante", message, StringComparison.Ordinal);
        Assert.Contains("Projeção", message, StringComparison.Ordinal);
        Assert.Contains("Por dia até fechar", message, StringComparison.Ordinal);
        Assert.Contains($"Vs {previousLabel}", message, StringComparison.Ordinal);
        Assert.Contains("Sugestão equilibrada", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Telegram_summary_includes_month_budget_context()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var auth = await app.RegisterAsync("telegram-summary-budget");
        await app.CreateReceitaAsync(auth.Token, "Salario", 4000m, true);
        await app.CreateGastoAsync(auth.Token, "Mercado", 180m);
        await app.UpdateMonthlyBudgetAsync(auth.Token, 900m);

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserContextAccessor>();
        currentUser.SetTelegramChatId(445566);
        currentUser.SetAuthenticatedUser(auth.UsuarioId, auth.Email);

        var processor = scope.ServiceProvider.GetRequiredService<IFinanceMessageProcessor>();
        var result = await processor.ProcessarMensagemAsync(new FinanceMessageRequest("resumo", 445566));
        var message = WebUtility.HtmlDecode(result.Mensagem);

        Assert.True(result.Sucesso);
        Assert.Contains("Resumo do dia", message, StringComparison.Ordinal);
        Assert.Contains("No mês", message, StringComparison.Ordinal);
        Assert.Contains("restam", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Raw_sql_respects_rls_and_blocks_cross_user_insert()
    {
        await using var app = await TestAppInstance.CreateAsync(_postgres);

        var firstUser = await app.RegisterAsync("charlie");
        var secondUser = await app.RegisterAsync("diana");

        await app.CreateGastoAsync(firstUser.Token, "Uber Charlie", 15m);
        await app.CreateGastoAsync(secondUser.Token, "Uber Diana", 18m);

        await using var userConnection = new NpgsqlConnection(app.ConnectionString);
        await userConnection.OpenAsync();

        await SetCurrentUserAsync(userConnection, firstUser.UsuarioId);

        await using (var listCommand = userConnection.CreateCommand())
        {
            listCommand.CommandText = "SELECT string_agg(\"Descricao\", ',') FROM public.\"Transacoes\"";
            var descriptions = (string?)await listCommand.ExecuteScalarAsync();

            Assert.Equal("Uber Charlie", descriptions);
        }

        await using (var insertCommand = userConnection.CreateCommand())
        {
            insertCommand.CommandText =
                """
                INSERT INTO public."Transacoes" ("Id", "UsuarioId", "Descricao", "Valor", "Data", "Categoria", "Origem")
                VALUES (@id, @usuario_id, @descricao, @valor, @data, @categoria, @origem)
                """;
            insertCommand.Parameters.AddWithValue("id", Guid.NewGuid());
            insertCommand.Parameters.AddWithValue("usuario_id", secondUser.UsuarioId);
            insertCommand.Parameters.AddWithValue("descricao", "Tentativa indevida");
            insertCommand.Parameters.AddWithValue("valor", 99m);
            insertCommand.Parameters.AddWithValue("data", DateTime.UtcNow);
            insertCommand.Parameters.AddWithValue("categoria", "Outros");
            insertCommand.Parameters.AddWithValue("origem", "Web");

            await Assert.ThrowsAsync<PostgresException>(() => insertCommand.ExecuteNonQueryAsync());
        }
    }

    private static async Task SetCurrentUserAsync(NpgsqlConnection connection, Guid userId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.current_user_id', @user_id, false)";
        command.Parameters.AddWithValue("user_id", userId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestAppInstance : IAsyncDisposable
    {
        private readonly PostgresContainerFixture _postgres;

        private TestAppInstance(
            PostgresContainerFixture postgres,
            string connectionString,
            FinanceBotWebApplicationFactory factory,
            HttpClient client)
        {
            _postgres = postgres;
            ConnectionString = connectionString;
            Factory = factory;
            Client = client;
        }

        public string ConnectionString { get; }
        public FinanceBotWebApplicationFactory Factory { get; }
        public HttpClient Client { get; }

        public static async Task<TestAppInstance> CreateAsync(PostgresContainerFixture postgres)
        {
            var connectionString = await postgres.CreateDatabaseAsync();
            var factory = new FinanceBotWebApplicationFactory(connectionString);
            var client = factory.CreateClient();

            var healthResponse = await client.GetAsync("/health");
            healthResponse.EnsureSuccessStatusCode();

            return new TestAppInstance(postgres, connectionString, factory, client);
        }

        public async Task<AuthResponse> RegisterAsync(string? prefix = null)
        {
            var emailPrefix = prefix ?? "user";
            var payload = new
            {
                Email = $"{emailPrefix}.{Guid.NewGuid():N}@example.com",
                Senha = "Teste@123456"
            };

            var response = await Client.PostAsJsonAsync("/auth/register", payload);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(auth);
            Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
            return auth!;
        }

        public async Task<CodigoVinculoResponse> GenerateLinkCodeAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/gerar-vinculo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Link code request failed with {(int)response.StatusCode}: {body}");
            }

            var code = await response.Content.ReadFromJsonAsync<CodigoVinculoResponse>();
            Assert.NotNull(code);
            return code!;
        }

        public async Task<GastoResponse> CreateGastoAsync(
            string token,
            string descricao,
            decimal valor,
            string? observacao = null,
            bool? ehFixo = null,
            bool? ehEssencial = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/gastos")
            {
                Content = JsonContent.Create(new
                {
                    Descricao = descricao,
                    Valor = valor,
                    Observacao = observacao,
                    EhFixo = ehFixo,
                    EhEssencial = ehEssencial
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Created)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Expected 201 Created but got {(int)response.StatusCode}: {body}");
            }

            var gasto = await response.Content.ReadFromJsonAsync<GastoResponse>();
            Assert.NotNull(gasto);
            return gasto!;
        }

        public async Task<ReceitaResponse> CreateReceitaAsync(string token, string descricao, decimal valor, bool ehFixo, string? observacao = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/receitas")
            {
                Content = JsonContent.Create(new { Descricao = descricao, Valor = valor, EhFixo = ehFixo, Observacao = observacao })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Created)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Expected 201 Created but got {(int)response.StatusCode}: {body}");
            }

            var receita = await response.Content.ReadFromJsonAsync<ReceitaResponse>();
            Assert.NotNull(receita);
            return receita!;
        }

        public async Task<BillingStatusResponse> GetBillingStatusAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/billing/status");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Billing request failed with {(int)response.StatusCode}: {body}");
            }

            var status = await response.Content.ReadFromJsonAsync<BillingStatusResponse>();
            Assert.NotNull(status);
            return status!;
        }

        public async Task<SolicitacaoUpgradeResponse> RequestUpgradeAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/solicitar-upgrade");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Upgrade request failed with {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<SolicitacaoUpgradeResponse>();
            Assert.NotNull(result);
            return result!;
        }

        public async Task<List<MovimentoResponse>> GetMovimentosAsync(string token)
        {
            return await ListMovimentosAsync(token, "/api/movimentos/ultimos");
        }

        public async Task<List<MovimentoResponse>> ListMovimentosAsync(string token, string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var movimentos = await response.Content.ReadFromJsonAsync<List<MovimentoResponse>>();
            Assert.NotNull(movimentos);
            return movimentos!;
        }

        public async Task<List<GastoResponse>> GetGastosAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gastos");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var gastos = await response.Content.ReadFromJsonAsync<List<GastoResponse>>();
            Assert.NotNull(gastos);
            return gastos!;
        }

        public async Task<RelatorioMensalResponse> GetMonthlyReportAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/relatorios/mensal");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Monthly report request failed with {(int)response.StatusCode}: {body}");
            }

            var report = await response.Content.ReadFromJsonAsync<RelatorioMensalResponse>();
            Assert.NotNull(report);
            return report!;
        }

        public async Task<OrcamentoMensalResponse> GetMonthlyBudgetAsync(string token, int? ano = null, int? mes = null)
        {
            var path = "/api/orcamentos/mensal";
            if (ano.HasValue || mes.HasValue)
            {
                var parameters = new List<string>();
                if (ano.HasValue)
                {
                    parameters.Add($"ano={ano.Value}");
                }

                if (mes.HasValue)
                {
                    parameters.Add($"mes={mes.Value}");
                }

                path = $"{path}?{string.Join("&", parameters)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Monthly budget request failed with {(int)response.StatusCode}: {body}");
            }

            var budget = await response.Content.ReadFromJsonAsync<OrcamentoMensalResponse>();
            Assert.NotNull(budget);
            return budget!;
        }

        public async Task<OrcamentoMensalResponse> UpdateMonthlyBudgetAsync(string token, decimal limiteGastos, int? ano = null, int? mes = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, "/api/orcamentos/mensal")
            {
                Content = JsonContent.Create(new
                {
                    LimiteGastos = limiteGastos,
                    Ano = ano,
                    Mes = mes
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Monthly budget update failed with {(int)response.StatusCode}: {body}");
            }

            var budget = await response.Content.ReadFromJsonAsync<OrcamentoMensalResponse>();
            Assert.NotNull(budget);
            return budget!;
        }

        public async Task<DesvinculoTelegramResponse> UnlinkAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/desvincular");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Unlink request failed with {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<DesvinculoTelegramResponse>();
            Assert.NotNull(result);
            return result!;
        }

        public async Task<GastoResponse> UpdateGastoAsync(
            string token,
            Guid gastoId,
            string descricao,
            decimal valor,
            DateOnly data,
            string categoria,
            string? observacao = null,
            bool? ehFixo = null,
            bool? ehEssencial = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/gastos/{gastoId}")
            {
                Content = JsonContent.Create(new
                {
                    Descricao = descricao,
                    Valor = valor,
                    Data = data,
                    Categoria = categoria,
                    Observacao = observacao,
                    EhFixo = ehFixo,
                    EhEssencial = ehEssencial
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Update gasto request failed with {(int)response.StatusCode}: {body}");
            }

            var gasto = await response.Content.ReadFromJsonAsync<GastoResponse>();
            Assert.NotNull(gasto);
            return gasto!;
        }

        public async Task<ReceitaResponse> UpdateReceitaAsync(
            string token,
            Guid receitaId,
            string descricao,
            decimal valor,
            DateOnly data,
            bool ehFixo,
            string? observacao = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/receitas/{receitaId}")
            {
                Content = JsonContent.Create(new { Descricao = descricao, Valor = valor, Data = data, EhFixo = ehFixo, Observacao = observacao })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Update receita request failed with {(int)response.StatusCode}: {body}");
            }

            var receita = await response.Content.ReadFromJsonAsync<ReceitaResponse>();
            Assert.NotNull(receita);
            return receita!;
        }

        public async Task DeleteGastoAsync(string token, Guid gastoId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/gastos/{gastoId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Delete gasto request failed with {(int)response.StatusCode}: {body}");
            }
        }

        public async Task DeleteReceitaAsync(string token, Guid receitaId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/receitas/{receitaId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Delete receita request failed with {(int)response.StatusCode}: {body}");
            }
        }

        public async Task DisableTrialAsync(Guid usuarioId)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE public."AssinaturasUsuario"
                SET "TrialAteUtc" = NULL,
                    "AtualizadoEmUtc" = NOW()
                WHERE "UsuarioId" = @usuario_id
                """;
            command.Parameters.AddWithValue("usuario_id", usuarioId);
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            await _postgres.DropDatabaseAsync(ConnectionString);
        }
    }

    private sealed record AuthResponse(Guid UsuarioId, string Email, string Token, DateTime ExpiraEmUtc);

    private sealed record CodigoVinculoResponse(string Codigo, DateTime ExpiraEmUtc);

    private sealed record BillingStatusResponse(
        string PlanoAtual,
        string PlanoEfetivo,
        string StatusAssinatura,
        DateTime? TrialAteUtc,
        DateTime? UpgradeSolicitadoEmUtc,
        bool TrialAtivo,
        int? DiasRestantesTrial,
        string MensagemStatus,
        int LancamentosNoMesAtual,
        int? LimiteLancamentosNoMesAtual,
        bool PodeRegistrarLancamento,
        bool UpgradePendente,
        bool PodeSolicitarUpgrade,
        string? MotivoBloqueio,
        string? MensagemUpgrade);

    private sealed record SolicitacaoUpgradeResponse(
        bool Sucesso,
        string Mensagem,
        DateTime? SolicitadoEmUtc,
        bool UpgradePendente);

    private sealed record MovimentoResponse(
        Guid Id,
        string Tipo,
        string Descricao,
        string? Categoria,
        bool? EhFixo,
        bool? EhEssencial,
        string Origem,
        string? Observacao);

    private sealed record GastoResponse(
        Guid Id,
        string Descricao,
        decimal Valor,
        DateTime Data,
        string Categoria,
        bool EhFixo,
        bool EhEssencial,
        string Origem,
        string? Observacao);

    private sealed record ReceitaResponse(Guid Id, string Descricao, decimal Valor, DateTime Data, bool EhFixo, string Origem, string? Observacao);

    private sealed record RelatorioMensalResponse(
        int Ano,
        int Mes,
        decimal TotalReceitas,
        decimal TotalGastos,
        decimal Saldo,
        int TotalLancamentos,
        List<CategoriaResumoResponse> TopCategoriasGasto);

    private sealed record OrcamentoMensalResponse(
        int Ano,
        int Mes,
        decimal? LimiteGastos,
        decimal TotalGastos,
        decimal TotalReceitas,
        decimal GastoFixo,
        decimal GastoEssencial,
        decimal GastoNaoEssencial,
        decimal? Restante,
        decimal? PercentualConsumido,
        decimal ProjecaoFechamento,
        decimal? DiferencaProjetada,
        int DiasNoMes,
        int DiasDecorridos,
        int DiasRestantes,
        bool PossuiOrcamentoDefinido,
        bool Estourado,
        bool EstouroProjetado,
        decimal? SugestaoLimiteSeguro,
        decimal? SugestaoLimiteEquilibrado,
        decimal? SugestaoLimiteFlexivel,
        int MesesBaseSugestao);

    private sealed record CategoriaResumoResponse(string Categoria, decimal TotalGasto, int Quantidade);

    private sealed record DesvinculoTelegramResponse(bool Sucesso, string Mensagem);
}
