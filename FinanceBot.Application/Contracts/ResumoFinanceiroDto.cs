namespace FinanceBot.Application.Contracts;

public sealed record ResumoFinanceiroDto(DateOnly Data, decimal Ganhos, decimal Gastos, decimal Saldo);
