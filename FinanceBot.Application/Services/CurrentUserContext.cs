using FinanceBot.Application.Contracts;

namespace FinanceBot.Application.Services;

public sealed class CurrentUserContext : ICurrentUserContextAccessor
{
    public Guid? UsuarioId { get; private set; }
    public string? Email { get; private set; }
    public long? TelegramChatId { get; private set; }
    public bool IsAuthenticated => UsuarioId.HasValue;

    public void SetAuthenticatedUser(Guid usuarioId, string email)
    {
        UsuarioId = usuarioId;
        Email = email;
    }

    public void SetTelegramChatId(long chatId)
    {
        TelegramChatId = chatId;
    }

    public void ClearAuthenticatedUser()
    {
        UsuarioId = null;
        Email = null;
    }
}
