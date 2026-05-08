namespace FinanceBot.Application.Contracts;

public sealed record ListarMovimentosRequest(
    DateOnly? Inicio = null,
    DateOnly? Fim = null,
    string? Tipo = null,
    string? Busca = null,
    string? Categoria = null,
    string? Origem = null,
    int Limite = 100);
