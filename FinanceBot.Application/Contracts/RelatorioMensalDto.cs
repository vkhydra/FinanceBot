namespace FinanceBot.Application.Contracts;

public sealed record RelatorioMensalDto(
    int Ano,
    int Mes,
    decimal TotalReceitas,
    decimal TotalGastos,
    decimal Saldo,
    int TotalLancamentos,
    IReadOnlyList<CategoriaResumoDto> TopCategoriasGasto);
