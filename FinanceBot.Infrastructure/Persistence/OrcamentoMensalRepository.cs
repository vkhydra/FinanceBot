using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class OrcamentoMensalRepository : IOrcamentoMensalRepository
{
    private readonly AppDbContext _db;

    public OrcamentoMensalRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(OrcamentoMensal orcamentoMensal, CancellationToken cancellationToken = default)
    {
        await _db.OrcamentosMensais.AddAsync(orcamentoMensal, cancellationToken);
    }

    public async Task<OrcamentoMensal?> GetByPeriodoAsync(int ano, int mes, CancellationToken cancellationToken = default)
    {
        return await _db.OrcamentosMensais
            .FirstOrDefaultAsync(
                item => item.Ano == ano && item.Mes == mes,
                cancellationToken);
    }
}
