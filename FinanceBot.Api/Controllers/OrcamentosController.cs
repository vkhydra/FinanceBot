using FinanceBot.Api.Requests;
using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orcamentos")]
public sealed class OrcamentosController : ControllerBase
{
    [HttpGet("mensal")]
    [ProducesResponseType(typeof(OrcamentoMensalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrcamentoMensalDto>> ObterMensal(
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

        var orcamento = await financeOperationsService.ObterOrcamentoMensalAsync(ano, mes, cancellationToken);
        return Ok(orcamento);
    }

    [HttpPut("mensal")]
    [ProducesResponseType(typeof(OrcamentoMensalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrcamentoMensalDto>> AtualizarMensal(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromBody] AtualizarOrcamentoMensalHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var orcamento = await financeOperationsService.AtualizarOrcamentoMensalAsync(
            new AtualizarOrcamentoMensalRequest(request.Ano, request.Mes, request.LimiteGastos),
            cancellationToken);

        return Ok(orcamento);
    }
}
