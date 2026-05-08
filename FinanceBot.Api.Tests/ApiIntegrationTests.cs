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
        Assert.Contains(gastos, item => item.Descricao == "CAFÉ da manhã" && item.Categoria == "Alimentacao");
        Assert.Contains(movimentos, item => item.Descricao == "CAFÉ da manhã" && item.Categoria == "Alimentacao");
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
                INSERT INTO public."Transacoes" ("Id", "UsuarioId", "Descricao", "Valor", "Data", "Categoria")
                VALUES (@id, @usuario_id, @descricao, @valor, @data, @categoria)
                """;
            insertCommand.Parameters.AddWithValue("id", Guid.NewGuid());
            insertCommand.Parameters.AddWithValue("usuario_id", secondUser.UsuarioId);
            insertCommand.Parameters.AddWithValue("descricao", "Tentativa indevida");
            insertCommand.Parameters.AddWithValue("valor", 99m);
            insertCommand.Parameters.AddWithValue("data", DateTime.UtcNow);
            insertCommand.Parameters.AddWithValue("categoria", "Outros");

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

        public async Task<GastoResponse> CreateGastoAsync(string token, string descricao, decimal valor)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/gastos")
            {
                Content = JsonContent.Create(new { Descricao = descricao, Valor = valor })
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

        public async Task CreateReceitaAsync(string token, string descricao, decimal valor, bool ehFixo)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/receitas")
            {
                Content = JsonContent.Create(new { Descricao = descricao, Valor = valor, EhFixo = ehFixo })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Created)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Expected 201 Created but got {(int)response.StatusCode}: {body}");
            }
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
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/movimentos/ultimos");
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

    private sealed record MovimentoResponse(string Tipo, string Descricao, string? Categoria);

    private sealed record GastoResponse(Guid Id, string Descricao, decimal Valor, DateTime Data, string Categoria);

    private sealed record RelatorioMensalResponse(
        int Ano,
        int Mes,
        decimal TotalReceitas,
        decimal TotalGastos,
        decimal Saldo,
        int TotalLancamentos,
        List<CategoriaResumoResponse> TopCategoriasGasto);

    private sealed record CategoriaResumoResponse(string Categoria, decimal TotalGasto, int Quantidade);

    private sealed record DesvinculoTelegramResponse(bool Sucesso, string Mensagem);
}
