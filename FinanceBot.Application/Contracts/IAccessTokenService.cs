namespace FinanceBot.Application.Contracts;

public interface IAccessTokenService
{
    AccessTokenResult Generate(Guid usuarioId, string email);
}
