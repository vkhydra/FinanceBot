namespace FinanceBot.Application.Contracts;

public sealed record GastoClassification(
    string Categoria,
    bool EhEssencial,
    bool EhFixo);
