using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/billing")]
public sealed class BillingController : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(BillingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BillingStatusDto>> ObterStatus(
        [FromServices] IAccessPolicyService accessPolicyService,
        CancellationToken cancellationToken = default)
    {
        var status = await accessPolicyService.ObterStatusAtualAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("solicitar-upgrade")]
    [ProducesResponseType(typeof(SolicitacaoUpgradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SolicitacaoUpgradeDto>> SolicitarUpgrade(
        [FromServices] IAccessPolicyService accessPolicyService,
        CancellationToken cancellationToken = default)
    {
        var resultado = await accessPolicyService.SolicitarUpgradeAsync(cancellationToken);
        return Ok(resultado);
    }
}
