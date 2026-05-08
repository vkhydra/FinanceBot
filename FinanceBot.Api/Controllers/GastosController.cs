using FinanceBot.Api.Requests;
using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/gastos")]
public sealed class GastosController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GastoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GastoDto>>> Listar(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromQuery] int limite = 20,
        CancellationToken cancellationToken = default)
    {
        var gastos = await financeOperationsService.ListarGastosAsync(limite, cancellationToken);
        return Ok(gastos);
    }

    [HttpPost]
    [ProducesResponseType(typeof(GastoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GastoDto>> Criar(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromBody] CriarGastoHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var gasto = await financeOperationsService.RegistrarGastoAsync(
            new CriarGastoRequest(request.Descricao, request.Valor),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, gasto);
    }
}
