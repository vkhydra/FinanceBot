namespace FinanceBot.Application.Contracts;

public sealed record MovimentoDto(
    Guid Id,
    string Tipo,
    string Descricao,
    decimal Valor,
    DateTime Data,
    string? Categoria,
    bool? EhFixo,
    string Origem,
    string? Observacao);
