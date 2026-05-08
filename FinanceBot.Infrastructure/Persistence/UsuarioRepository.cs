using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _db;

    public UsuarioRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        await _db.Usuarios.AddAsync(usuario, cancellationToken);
    }

    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Usuarios.FirstOrDefaultAsync(usuario => usuario.Id == id, cancellationToken);
    }

    public async Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _db.Usuarios.FirstOrDefaultAsync(usuario => usuario.Email == email, cancellationToken);
    }

    public async Task<Usuario?> GetByTelegramIdAsync(long telegramId, CancellationToken cancellationToken = default)
    {
        return await _db.Usuarios.FirstOrDefaultAsync(usuario => usuario.TelegramId == telegramId, cancellationToken);
    }

    public async Task<Usuario?> GetByCodigoVinculoAsync(string codigoVinculo, CancellationToken cancellationToken = default)
    {
        return await _db.Usuarios.FirstOrDefaultAsync(usuario => usuario.CodigoVinculo == codigoVinculo, cancellationToken);
    }
}
