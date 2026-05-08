namespace FinanceBot.Application.Contracts;

public interface ICurrentUserContext
{
    Guid? UsuarioId { get; }
    string? Email { get; }
    long? TelegramChatId { get; }
    bool IsAuthenticated { get; }
}

public interface ICurrentUserContextAccessor : ICurrentUserContext
{
    void SetAuthenticatedUser(Guid usuarioId, string email);
    void SetTelegramChatId(long chatId);
    void ClearAuthenticatedUser();
}
