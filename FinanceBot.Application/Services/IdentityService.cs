using System.Security.Cryptography;
using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;

namespace FinanceBot.Application.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenService _accessTokenService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAssinaturaUsuarioRepository _assinaturas;
    private readonly IFinanceUnitOfWork _unitOfWork;

    public IdentityService(
        IUsuarioRepository usuarios,
        IPasswordHasher passwordHasher,
        IAccessTokenService accessTokenService,
        ICurrentUserContext currentUserContext,
        IAssinaturaUsuarioRepository assinaturas,
        IFinanceUnitOfWork unitOfWork)
    {
        _usuarios = usuarios;
        _passwordHasher = passwordHasher;
        _accessTokenService = accessTokenService;
        _currentUserContext = currentUserContext;
        _assinaturas = assinaturas;
        _unitOfWork = unitOfWork;
    }

    public async Task<AutenticacaoDto> RegistrarAsync(RegistrarUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var emailNormalizado = NormalizeEmail(request.Email);
        var senha = request.Senha.Trim();

        if (await _usuarios.GetByEmailAsync(emailNormalizado, cancellationToken) is not null)
        {
            throw new InvalidOperationException("Já existe um usuário cadastrado com este e-mail.");
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = emailNormalizado,
            SenhaHash = _passwordHasher.Hash(senha)
        };
        var assinatura = new AssinaturaUsuario
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            PlanoAtual = PlanoUsuario.Free,
            StatusAssinatura = StatusAssinatura.Nenhuma,
            AtualizadoEmUtc = DateTime.UtcNow
        };

        await _usuarios.AddAsync(usuario, cancellationToken);
        await _assinaturas.AddAsync(assinatura, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BuildAutenticacao(usuario);
    }

    public async Task<AutenticacaoDto?> LoginAsync(LoginUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var emailNormalizado = NormalizeEmail(request.Email);
        var usuario = await _usuarios.GetByEmailAsync(emailNormalizado, cancellationToken);
        if (usuario is null || !_passwordHasher.Verify(request.Senha.Trim(), usuario.SenhaHash))
        {
            return null;
        }

        return BuildAutenticacao(usuario);
    }

    public async Task<CodigoVinculoDto> GerarCodigoVinculoAsync(CancellationToken cancellationToken = default)
    {
        var usuarioId = _currentUserContext.UsuarioId
            ?? throw new InvalidOperationException("Não existe usuário autenticado para gerar o vínculo.");

        var usuario = await _usuarios.GetByIdAsync(usuarioId, cancellationToken)
            ?? throw new InvalidOperationException("Usuário autenticado não encontrado.");

        var utcNow = DateTime.UtcNow;
        var codigoVinculo = await GenerateUniqueCodeAsync(cancellationToken);

        usuario.CodigoVinculo = codigoVinculo;
        usuario.VinculoExpiracao = utcNow.AddMinutes(10);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CodigoVinculoDto(codigoVinculo, usuario.VinculoExpiracao.Value);
    }

    public async Task<UsuarioAutenticado?> ObterUsuarioPorTelegramAsync(long telegramId, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.GetByTelegramIdAsync(telegramId, cancellationToken);
        return usuario is null
            ? null
            : new UsuarioAutenticado(usuario.Id, usuario.Email);
    }

    public async Task<VinculoTelegramResult> VincularTelegramAsync(long telegramId, string codigoVinculo, CancellationToken cancellationToken = default)
    {
        var codigoNormalizado = codigoVinculo.Trim();
        if (string.IsNullOrWhiteSpace(codigoNormalizado))
        {
            return new VinculoTelegramResult(false, "Informe um código de vínculo no formato /vincular 123456.");
        }

        var usuarioDoChat = await _usuarios.GetByTelegramIdAsync(telegramId, cancellationToken);
        if (usuarioDoChat is not null)
        {
            return new VinculoTelegramResult(false, "Este chat já está vinculado a uma conta.");
        }

        var usuario = await _usuarios.GetByCodigoVinculoAsync(codigoNormalizado, cancellationToken);
        if (usuario is null || usuario.VinculoExpiracao is null || usuario.VinculoExpiracao <= DateTime.UtcNow)
        {
            return new VinculoTelegramResult(false, "Código de vínculo inválido ou expirado.");
        }

        if (usuario.TelegramId.HasValue && usuario.TelegramId.Value != telegramId)
        {
            return new VinculoTelegramResult(false, "Esta conta já está vinculada a outro chat do Telegram.");
        }

        usuario.TelegramId = telegramId;
        usuario.CodigoVinculo = null;
        usuario.VinculoExpiracao = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VinculoTelegramResult(true, "✅ Chat vinculado com sucesso. Agora você já pode registrar gastos e receitas por aqui.");
    }

    private AutenticacaoDto BuildAutenticacao(Usuario usuario)
    {
        var token = _accessTokenService.Generate(usuario.Id, usuario.Email);
        return new AutenticacaoDto(usuario.Id, usuario.Email, token.Token, token.ExpiraEmUtc);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            if (await _usuarios.GetByCodigoVinculoAsync(codigo, cancellationToken) is null)
            {
                return codigo;
            }
        }

        throw new InvalidOperationException("Não foi possível gerar um código de vínculo único.");
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
