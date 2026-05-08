using FinanceBot.Domain.Entities;

namespace FinanceBot.Application.Contracts;

public interface IReceitaRepository
{
    Task AddAsync(Receita receita, CancellationToken cancellationToken = default);
    Task<Receita?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountInPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken cancellationToken = default);
    Task<decimal> SumByDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Receita>> ListInPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Receita>> ListRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<Receita?> GetLatestAsync(CancellationToken cancellationToken = default);
    void Remove(Receita receita);
}
