using FinanceBot.Domain.Enums;

namespace FinanceBot.Domain.Entities;

public sealed class AssinaturaUsuario
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public PlanoUsuario PlanoAtual { get; set; } = PlanoUsuario.Free;
    public StatusAssinatura StatusAssinatura { get; set; } = StatusAssinatura.Nenhuma;
    public DateTime? PremiumAteUtc { get; set; }
    public DateTime? TrialAteUtc { get; set; }
    public string? GatewayCustomerId { get; set; }
    public string? GatewaySubscriptionId { get; set; }
    public DateTime AtualizadoEmUtc { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
