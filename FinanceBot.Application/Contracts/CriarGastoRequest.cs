namespace FinanceBot.Application.Contracts;

public sealed record CriarGastoRequest(
    string Descricao,
    decimal Valor,
    string? Observacao = null,
    bool? EhFixo = null,
    bool? EhEssencial = null);
