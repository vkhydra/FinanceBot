using FinanceBot.Domain.Entities;

namespace FinanceBot.Application.Contracts;

public interface IUsuarioRepository
{
    Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByTelegramIdAsync(long telegramId, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByCodigoVinculoAsync(string codigoVinculo, CancellationToken cancellationToken = default);
}
