using FinanceBot.Domain.Entities;

namespace FinanceBot.Application.Contracts;

public interface IOrcamentoMensalRepository
{
    Task AddAsync(OrcamentoMensal orcamentoMensal, CancellationToken cancellationToken = default);
    Task<OrcamentoMensal?> GetByPeriodoAsync(int ano, int mes, CancellationToken cancellationToken = default);
}
