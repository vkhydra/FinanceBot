using FinanceBot.Api.Requests;
using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(AutenticacaoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AutenticacaoDto>> Register(
        [FromServices] IIdentityService identityService,
        [FromBody] RegistrarUsuarioHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var autenticacao = await identityService.RegistrarAsync(
                new RegistrarUsuarioRequest(request.Email, request.Senha),
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, autenticacao);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AutenticacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AutenticacaoDto>> Login(
        [FromServices] IIdentityService identityService,
        [FromBody] LoginUsuarioHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var autenticacao = await identityService.LoginAsync(
            new LoginUsuarioRequest(request.Email, request.Senha),
            cancellationToken);

        if (autenticacao is null)
        {
            return Unauthorized(new { message = "E-mail ou senha inválidos." });
        }

        return Ok(autenticacao);
    }

    [Authorize]
    [HttpPost("gerar-vinculo")]
    [ProducesResponseType(typeof(CodigoVinculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CodigoVinculoDto>> GerarVinculo(
        [FromServices] IIdentityService identityService,
        CancellationToken cancellationToken = default)
    {
        var codigo = await identityService.GerarCodigoVinculoAsync(cancellationToken);
        return Ok(codigo);
    }

    [Authorize]
    [HttpPost("desvincular")]
    [ProducesResponseType(typeof(DesvinculoTelegramResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DesvinculoTelegramResult>> Desvincular(
        [FromServices] IIdentityService identityService,
        CancellationToken cancellationToken = default)
    {
        var resultado = await identityService.DesvincularUsuarioAtualAsync(cancellationToken);
        return Ok(resultado);
    }
}
