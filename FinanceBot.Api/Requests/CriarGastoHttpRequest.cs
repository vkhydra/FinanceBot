using System.ComponentModel.DataAnnotations;

namespace FinanceBot.Api.Requests;

public sealed class CriarGastoHttpRequest
{
    [Required]
    [MinLength(1)]
    public string Descricao { get; init; } = string.Empty;

    [Range(0.01d, double.MaxValue)]
    public decimal Valor { get; init; }

    [MaxLength(500)]
    public string? Observacao { get; init; }

    public bool? EhFixo { get; init; }

    public bool? EhEssencial { get; init; }
}
