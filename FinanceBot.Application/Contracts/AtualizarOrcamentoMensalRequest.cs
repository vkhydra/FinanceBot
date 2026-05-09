namespace FinanceBot.Application.Contracts;

public sealed record AtualizarOrcamentoMensalRequest(int? Ano, int? Mes, decimal LimiteGastos);
