namespace FinanceBot.Application.Contracts;

public sealed record FinanceMessageResult(bool Sucesso, string Mensagem, bool UsaHtml = false)
{
    public static FinanceMessageResult Ok(string mensagem) => new(true, mensagem);

    public static FinanceMessageResult Falha(string mensagem) => new(false, mensagem);

    public static FinanceMessageResult OkHtml(string mensagem) => new(true, mensagem, true);

    public static FinanceMessageResult FalhaHtml(string mensagem) => new(false, mensagem, true);
}
