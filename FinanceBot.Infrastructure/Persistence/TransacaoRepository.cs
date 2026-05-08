using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class TransacaoRepository : ITransacaoRepository
{
    private readonly AppDbContext _db;

    public TransacaoRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Transacao transacao, CancellationToken cancellationToken = default)
    {
        await _db.Transacoes.AddAsync(transacao, cancellationToken);
    }

    public async Task<int> CountInPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken cancellationToken = default)
    {
        return await _db.Transacoes
            .CountAsync(transacao => transacao.Data >= startUtc && transacao.Data < endExclusiveUtc, cancellationToken);
    }

    public async Task<Transacao?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Transacoes
            .OrderByDescending(t => t.Data)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transacao>> ListRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _db.Transacoes
            .OrderByDescending(t => t.Data)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public void Remove(Transacao transacao)
    {
        _db.Transacoes.Remove(transacao);
    }

    public async Task<decimal> SumByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        return await _db.Transacoes
            .Where(t => t.Data.Date == utcDate)
            .SumAsync(t => t.Valor, cancellationToken);
    }
}
