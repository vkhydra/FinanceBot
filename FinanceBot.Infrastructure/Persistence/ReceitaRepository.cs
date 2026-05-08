using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class ReceitaRepository : IReceitaRepository
{
    private readonly AppDbContext _db;

    public ReceitaRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Receita receita, CancellationToken cancellationToken = default)
    {
        await _db.Receitas.AddAsync(receita, cancellationToken);
    }

    public async Task<int> CountInPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken cancellationToken = default)
    {
        return await _db.Receitas
            .CountAsync(receita => receita.Data >= startUtc && receita.Data < endExclusiveUtc, cancellationToken);
    }

    public async Task<Receita?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Receitas
            .OrderByDescending(r => r.Data)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Receita>> ListRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _db.Receitas
            .OrderByDescending(r => r.Data)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public void Remove(Receita receita)
    {
        _db.Receitas.Remove(receita);
    }

    public async Task<decimal> SumByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        return await _db.Receitas
            .Where(r => r.Data.Date == utcDate)
            .SumAsync(r => r.Valor, cancellationToken);
    }
}
