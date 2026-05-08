namespace FinanceBot.Application.Contracts;

public sealed record AtualizarReceitaRequest(
    string Descricao,
    decimal Valor,
    DateOnly Data,
    bool EhFixo,
    string? Observacao = null);
