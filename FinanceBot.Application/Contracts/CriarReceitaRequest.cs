namespace FinanceBot.Application.Contracts;

public sealed record CriarReceitaRequest(string Descricao, decimal Valor, bool EhFixo, string? Observacao = null);
