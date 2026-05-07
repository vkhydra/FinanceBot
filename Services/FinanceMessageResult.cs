namespace FinanceBot.Services;

public sealed record FinanceMessageResult(bool Sucesso, string Mensagem)
{
    public static FinanceMessageResult Ok(string mensagem) => new(true, mensagem);

    public static FinanceMessageResult Falha(string mensagem) => new(false, mensagem);
}
