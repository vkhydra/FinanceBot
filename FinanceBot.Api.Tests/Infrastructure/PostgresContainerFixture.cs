using Npgsql;
using Testcontainers.PostgreSql;

namespace FinanceBot.Api.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "postgres";
}

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string AdminConnectionString =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "postgres"
        }.ConnectionString;

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> CreateDatabaseAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var databaseName = $"financebot_test_{suffix[..20]}";
        var roleName = $"financebot_app_{suffix[20..30]}";
        var password = $"Teste!{suffix}";

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();

        await using (var createRoleCommand = connection.CreateCommand())
        {
            var escapedPassword = password.Replace("'", "''", StringComparison.Ordinal);
            createRoleCommand.CommandText =
                $"""CREATE ROLE "{roleName}" WITH LOGIN PASSWORD '{escapedPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION;""";
            await createRoleCommand.ExecuteNonQueryAsync();
        }

        await using (var createDatabaseCommand = connection.CreateCommand())
        {
            createDatabaseCommand.CommandText = $"""CREATE DATABASE "{databaseName}" OWNER "{roleName}";""";
            await createDatabaseCommand.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName,
            Username = roleName,
            Password = password
        }.ConnectionString;
    }

    public async Task DropDatabaseAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database
            ?? throw new InvalidOperationException("O connection string de teste precisa informar o nome do banco.");
        var roleName = builder.Username
            ?? throw new InvalidOperationException("O connection string de teste precisa informar o usuário do banco.");

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();

        await using (var terminateCommand = connection.CreateCommand())
        {
            terminateCommand.CommandText =
                """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @database_name
                  AND pid <> pg_backend_pid();
                """;
            terminateCommand.Parameters.AddWithValue("database_name", databaseName);
            await terminateCommand.ExecuteNonQueryAsync();
        }

        await using (var dropDatabaseCommand = connection.CreateCommand())
        {
            dropDatabaseCommand.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName}";""";
            await dropDatabaseCommand.ExecuteNonQueryAsync();
        }

        await using (var dropRoleCommand = connection.CreateCommand())
        {
            dropRoleCommand.CommandText = $"""DROP ROLE IF EXISTS "{roleName}";""";
            await dropRoleCommand.ExecuteNonQueryAsync();
        }
    }
}
