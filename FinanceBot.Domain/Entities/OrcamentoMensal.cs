namespace FinanceBot.Domain.Entities;

public sealed class OrcamentoMensal
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public decimal LimiteGastos { get; set; }
    public DateTime AtualizadoEmUtc { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
