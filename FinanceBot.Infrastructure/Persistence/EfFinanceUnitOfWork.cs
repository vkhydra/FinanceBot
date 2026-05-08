using FinanceBot.Application.Contracts;
using FinanceBot.Infrastructure.Data;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class EfFinanceUnitOfWork : IFinanceUnitOfWork
{
    private readonly AppDbContext _db;

    public EfFinanceUnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
