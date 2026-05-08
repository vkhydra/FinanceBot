using System.ComponentModel.DataAnnotations;

namespace FinanceBot.Api.Requests;

public sealed class CriarGastoHttpRequest
{
    [Required]
    [MinLength(1)]
    public string Descricao { get; init; } = string.Empty;

    [Range(0.01d, double.MaxValue)]
    public decimal Valor { get; init; }
}
