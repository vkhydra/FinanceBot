using System.ComponentModel.DataAnnotations;

namespace FinanceBot.Api.Requests;

public sealed class LoginUsuarioHttpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Senha { get; init; } = string.Empty;
}
