using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/resumo")]
public sealed class ResumoController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ResumoFinanceiroDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumoFinanceiroDto>> Obter(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromQuery] DateOnly? data = null,
        CancellationToken cancellationToken = default)
    {
        var resumo = await financeOperationsService.ObterResumoAsync(data, cancellationToken);
        return Ok(resumo);
    }
}
