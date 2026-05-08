using System.Globalization;
using System.Text;
using FinanceBot.Application.Contracts;

namespace FinanceBot.Application.Services;

public sealed class GastoCategorizationService : IGastoCategorizationService
{
    private static readonly (string Categoria, string[] Keywords)[] Rules =
    [
        ("Mercado", ["mercado", "supermercado", "hortifruti", "feira", "sacolao", "atacadao", "assai"]),
        ("Alimentacao", ["ifood", "restaurante", "lanche", "almoco", "jantar", "cafe", "pizza", "hamburguer", "bar", "delivery"]),
        ("Transporte", ["uber", "99", "taxi", "gasolina", "combustivel", "onibus", "metro", "pedagio", "estacionamento"]),
        ("Moradia", ["aluguel", "condominio", "energia", "luz", "agua", "internet", "telefone", "celular", "gas", "manutencao"]),
        ("Saude", ["farmacia", "medico", "consulta", "exame", "dentista", "remedio", "academia", "hospital"]),
        ("Educacao", ["escola", "faculdade", "curso", "livro", "material escolar"]),
        ("Assinaturas", ["netflix", "spotify", "youtube", "prime video", "hbo", "disney", "game pass", "assinatura"]),
        ("Compras", ["amazon", "mercado livre", "shopee", "magalu", "americanas"]),
        ("Lazer", ["cinema", "show", "viagem", "hotel", "passeio", "ingresso"])
    ];

    public string Categorize(string descricao)
    {
        var normalizedDescription = Normalize(descricao);
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return "Outros";
        }

        foreach (var (categoria, keywords) in Rules)
        {
            if (keywords.Any(normalizedDescription.Contains))
            {
                return categoria;
            }
        }

        return "Outros";
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
