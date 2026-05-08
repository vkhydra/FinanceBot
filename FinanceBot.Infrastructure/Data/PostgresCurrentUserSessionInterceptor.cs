using System.Data.Common;
using FinanceBot.Application.Contracts;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace FinanceBot.Infrastructure.Data;

public sealed class PostgresCurrentUserSessionInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentUserContext _currentUserContext;

    public PostgresCurrentUserSessionInterceptor(ICurrentUserContext currentUserContext)
    {
        _currentUserContext = currentUserContext;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ConfigureSession(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ConfigureSessionAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void ConfigureSession(DbConnection connection)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        using var command = npgsqlConnection.CreateCommand();
        command.CommandText = "select set_config('app.current_user_id', @user_id, false);";
        command.Parameters.AddWithValue("user_id", _currentUserContext.UsuarioId?.ToString() ?? string.Empty);
        command.ExecuteNonQuery();
    }

    private async Task ConfigureSessionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        await using var command = npgsqlConnection.CreateCommand();
        command.CommandText = "select set_config('app.current_user_id', @user_id, false);";
        command.Parameters.AddWithValue("user_id", _currentUserContext.UsuarioId?.ToString() ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
