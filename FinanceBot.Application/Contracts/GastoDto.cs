namespace FinanceBot.Application.Contracts;

public sealed record GastoDto(
    Guid Id,
    string Descricao,
    decimal Valor,
    DateTime Data,
    string Categoria,
    bool EhFixo,
    bool EhEssencial,
    string Origem,
    string? Observacao);
