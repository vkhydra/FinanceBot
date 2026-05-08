using FinanceBot.Application.Contracts;
using FinanceBot.Infrastructure.Authentication;
using FinanceBot.Infrastructure.Data;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<PostgresCurrentUserSessionInterceptor>();
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection deve ser configurada.");

            options
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .AddInterceptors(serviceProvider.GetRequiredService<PostgresCurrentUserSessionInterceptor>());
        });

        services.AddScoped<IAssinaturaUsuarioRepository, AssinaturaUsuarioRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ITransacaoRepository, TransacaoRepository>();
        services.AddScoped<IReceitaRepository, ReceitaRepository>();
        services.AddScoped<IFinanceUnitOfWork, EfFinanceUnitOfWork>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IAccessTokenService, JwtAccessTokenService>();

        return services;
    }

    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
