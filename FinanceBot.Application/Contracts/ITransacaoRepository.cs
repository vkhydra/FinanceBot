using FinanceBot.Domain.Entities;

namespace FinanceBot.Application.Contracts;

public interface ITransacaoRepository
{
    Task AddAsync(Transacao transacao, CancellationToken cancellationToken = default);
    Task<Transacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountInPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken cancellationToken = default);
    Task<decimal> SumByDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transacao>> ListInPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transacao>> ListRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<Transacao?> GetLatestAsync(CancellationToken cancellationToken = default);
    void Remove(Transacao transacao);
}
