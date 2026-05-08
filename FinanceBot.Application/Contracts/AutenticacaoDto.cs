namespace FinanceBot.Application.Contracts;

public sealed record AutenticacaoDto(Guid UsuarioId, string Email, string Token, DateTime ExpiraEmUtc);
