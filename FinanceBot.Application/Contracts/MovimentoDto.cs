namespace FinanceBot.Application.Contracts;

public sealed record MovimentoDto(string Tipo, string Descricao, decimal Valor, DateTime Data, string? Categoria);
