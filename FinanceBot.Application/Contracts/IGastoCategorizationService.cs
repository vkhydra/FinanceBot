namespace FinanceBot.Application.Contracts;

public interface IGastoCategorizationService
{
    GastoClassification Classify(string descricao);

    string Categorize(string descricao);
}
