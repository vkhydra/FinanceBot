namespace FinanceBot.Application.Contracts;

public sealed record MovimentoDto(
    Guid Id,
    string Tipo,
    string Descricao,
    decimal Valor,
    DateTime Data,
    string? Categoria,
    bool? EhFixo,
    bool? EhEssencial,
    string Origem,
    string? Observacao);
