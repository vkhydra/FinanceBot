namespace FinanceBot.Application.Contracts;

public interface IIdentityService
{
    Task<AutenticacaoDto> RegistrarAsync(RegistrarUsuarioRequest request, CancellationToken cancellationToken = default);
    Task<AutenticacaoDto?> LoginAsync(LoginUsuarioRequest request, CancellationToken cancellationToken = default);
    Task<CodigoVinculoDto> GerarCodigoVinculoAsync(CancellationToken cancellationToken = default);
    Task<DesvinculoTelegramResult> DesvincularUsuarioAtualAsync(CancellationToken cancellationToken = default);
    Task<UsuarioAutenticado?> ObterUsuarioPorTelegramAsync(long telegramId, CancellationToken cancellationToken = default);
    Task<VinculoTelegramResult> VincularTelegramAsync(long telegramId, string codigoVinculo, CancellationToken cancellationToken = default);
    Task<DesvinculoTelegramResult> DesvincularTelegramAsync(long telegramId, CancellationToken cancellationToken = default);
}
