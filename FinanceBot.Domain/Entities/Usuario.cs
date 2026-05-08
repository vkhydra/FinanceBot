namespace FinanceBot.Domain.Entities;

public sealed class Usuario
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public long? TelegramId { get; set; }
    public string? CodigoVinculo { get; set; }
    public DateTime? VinculoExpiracao { get; set; }

    public AssinaturaUsuario Assinatura { get; set; } = null!;
    public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();
    public ICollection<Receita> Receitas { get; set; } = new List<Receita>();
}
