namespace FinanceBot.Domain.Entities;

public class Receita
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime Data { get; set; }
    public bool EhFixo { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
