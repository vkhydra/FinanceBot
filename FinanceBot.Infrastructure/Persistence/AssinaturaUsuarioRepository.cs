using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class AssinaturaUsuarioRepository : IAssinaturaUsuarioRepository
{
    private readonly AppDbContext _db;

    public AssinaturaUsuarioRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AssinaturaUsuario assinatura, CancellationToken cancellationToken = default)
    {
        await _db.AssinaturasUsuario.AddAsync(assinatura, cancellationToken);
    }

    public async Task<AssinaturaUsuario?> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        return await _db.AssinaturasUsuario
            .FirstOrDefaultAsync(assinatura => assinatura.UsuarioId == usuarioId, cancellationToken);
    }
}
