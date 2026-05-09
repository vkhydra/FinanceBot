using System.ComponentModel.DataAnnotations;

namespace FinanceBot.Api.Requests;

public sealed class AtualizarOrcamentoMensalHttpRequest
{
    [Range(2000, 2100)]
    public int? Ano { get; init; }

    [Range(1, 12)]
    public int? Mes { get; init; }

    [Range(0.01d, double.MaxValue)]
    public decimal LimiteGastos { get; init; }
}
