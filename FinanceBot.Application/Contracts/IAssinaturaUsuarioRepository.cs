using FinanceBot.Domain.Entities;

namespace FinanceBot.Application.Contracts;

public interface IAssinaturaUsuarioRepository
{
    Task AddAsync(AssinaturaUsuario assinatura, CancellationToken cancellationToken = default);
    Task<AssinaturaUsuario?> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
