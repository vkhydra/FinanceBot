namespace FinanceBot.Application.Contracts;

public interface IFinanceOperationsService
{
    Task<GastoDto> RegistrarGastoAsync(CriarGastoRequest request, CancellationToken cancellationToken = default);
    Task<ReceitaDto> RegistrarReceitaAsync(CriarReceitaRequest request, CancellationToken cancellationToken = default);
    Task<GastoDto?> AtualizarGastoAsync(Guid gastoId, AtualizarGastoRequest request, CancellationToken cancellationToken = default);
    Task<ReceitaDto?> AtualizarReceitaAsync(Guid receitaId, AtualizarReceitaRequest request, CancellationToken cancellationToken = default);
    Task<bool> ExcluirGastoAsync(Guid gastoId, CancellationToken cancellationToken = default);
    Task<bool> ExcluirReceitaAsync(Guid receitaId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GastoDto>> ListarGastosAsync(int limite = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReceitaDto>> ListarReceitasAsync(int limite = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MovimentoDto>> ListarMovimentosAsync(ListarMovimentosRequest request, CancellationToken cancellationToken = default);
    Task<ResumoFinanceiroDto> ObterResumoAsync(DateOnly? data = null, CancellationToken cancellationToken = default);
    Task<RelatorioMensalDto> ObterRelatorioMensalAsync(int? ano = null, int? mes = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MovimentoDto>> ListarUltimosMovimentosAsync(int limite = 5, CancellationToken cancellationToken = default);
    Task<DesfazerMovimentoResult?> DesfazerUltimaAcaoAsync(CancellationToken cancellationToken = default);
}
