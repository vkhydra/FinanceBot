using System.ComponentModel.DataAnnotations;

namespace FinanceBot.Api.Requests;

public sealed class AtualizarReceitaHttpRequest
{
    [Required]
    [MinLength(1)]
    public string Descricao { get; init; } = string.Empty;

    [Range(0.01d, double.MaxValue)]
    public decimal Valor { get; init; }

    [Required]
    public DateOnly Data { get; init; }

    public bool EhFixo { get; init; }

    [MaxLength(500)]
    public string? Observacao { get; init; }
}
