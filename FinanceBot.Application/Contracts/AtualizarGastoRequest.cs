namespace FinanceBot.Application.Contracts;

public sealed record AtualizarGastoRequest(
    string Descricao,
    decimal Valor,
    DateOnly Data,
    string Categoria,
    string? Observacao = null,
    bool? EhFixo = null,
    bool? EhEssencial = null);
