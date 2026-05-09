using System.Globalization;
using System.Text;
using FinanceBot.Application.Contracts;

namespace FinanceBot.Application.Services;

public sealed class GastoCategorizationService : IGastoCategorizationService
{
    private static readonly GastoClassification DefaultClassification = new("Outros", false, false);
    private static readonly (GastoClassification Classification, string[] Keywords)[] Rules =
    [
        (new GastoClassification("Mercado", true, false), ["mercado", "supermercado", "hortifruti", "feira", "sacolao", "atacadao", "assai"]),
        (new GastoClassification("Alimentacao", false, false), ["ifood", "restaurante", "lanche", "almoco", "jantar", "cafe", "pizza", "hamburguer", "bar", "delivery"]),
        (new GastoClassification("Transporte", true, false), ["uber", "99", "taxi", "gasolina", "combustivel", "onibus", "metro", "pedagio", "estacionamento"]),
        (new GastoClassification("Moradia", true, true), ["aluguel", "condominio", "energia", "luz", "agua", "internet", "telefone", "celular", "gas", "manutencao"]),
        (new GastoClassification("Saude", true, false), ["farmacia", "medico", "consulta", "exame", "dentista", "remedio", "academia", "hospital"]),
        (new GastoClassification("Educacao", true, true), ["escola", "faculdade", "curso", "livro", "material escolar"]),
        (new GastoClassification("Assinaturas", false, true), ["netflix", "spotify", "youtube", "prime video", "hbo", "disney", "game pass", "assinatura"]),
        (new GastoClassification("Compras", false, false), ["amazon", "mercado livre", "shopee", "magalu", "americanas"]),
        (new GastoClassification("Lazer", false, false), ["cinema", "show", "viagem", "hotel", "passeio", "ingresso"])
    ];

    public GastoClassification Classify(string descricao)
    {
        var normalizedDescription = Normalize(descricao);
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return DefaultClassification;
        }

        foreach (var (classification, keywords) in Rules)
        {
            if (keywords.Any(normalizedDescription.Contains))
            {
                return classification;
            }
        }

        return DefaultClassification;
    }

    public string Categorize(string descricao)
    {
        return Classify(descricao).Categoria;
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var normalized = trimmed.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
