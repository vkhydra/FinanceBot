using System.ComponentModel.DataAnnotations;

namespace FinanceBot.Api.Requests;

public sealed class AtualizarGastoHttpRequest
{
    [Required]
    [MinLength(1)]
    public string Descricao { get; init; } = string.Empty;

    [Range(0.01d, double.MaxValue)]
    public decimal Valor { get; init; }

    [Required]
    public DateOnly Data { get; init; }

    [Required]
    [MinLength(1)]
    public string Categoria { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Observacao { get; init; }

    public bool? EhFixo { get; init; }

    public bool? EhEssencial { get; init; }
}
