namespace FinanceBot.Application.Contracts;

public sealed record ReceitaDto(
    Guid Id,
    string Descricao,
    decimal Valor,
    DateTime Data,
    bool EhFixo,
    string Origem,
    string? Observacao);
