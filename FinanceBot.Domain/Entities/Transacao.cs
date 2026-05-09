using FinanceBot.Domain.Enums;

namespace FinanceBot.Domain.Entities;

public class Transacao
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime Data { get; set; }
    public string? Categoria { get; set; }
    public bool EhFixo { get; set; }
    public bool EhEssencial { get; set; }
    public string? Observacao { get; set; }
    public OrigemLancamento Origem { get; set; } = OrigemLancamento.Web;

    public Usuario Usuario { get; set; } = null!;
}
