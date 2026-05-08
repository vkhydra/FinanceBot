using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/relatorios")]
public sealed class RelatoriosController : ControllerBase
{
    [HttpGet("mensal")]
    [ProducesResponseType(typeof(RelatorioMensalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RelatorioMensalDto>> ObterMensal(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromQuery] int? ano = null,
        [FromQuery] int? mes = null,
        CancellationToken cancellationToken = default)
    {
        if (mes is < 1 or > 12)
        {
            return BadRequest(new { message = "O parâmetro mes deve estar entre 1 e 12." });
        }

        if (ano is < 2000 or > 2100)
        {
            return BadRequest(new { message = "O parâmetro ano deve estar entre 2000 e 2100." });
        }

        var relatorio = await financeOperationsService.ObterRelatorioMensalAsync(ano, mes, cancellationToken);
        return Ok(relatorio);
    }
}
