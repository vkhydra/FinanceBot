namespace FinanceBot.Application.Contracts;

public interface IFinanceUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
