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
            new CriarGastoRequest(request.Descricao, request.Valor, request.Observacao),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, gasto);
    }

    [HttpPut("{gastoId:guid}")]
    [ProducesResponseType(typeof(GastoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GastoDto>> Atualizar(
        Guid gastoId,
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromBody] AtualizarGastoHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var gasto = await financeOperationsService.AtualizarGastoAsync(
            gastoId,
            new AtualizarGastoRequest(request.Descricao, request.Valor, request.Data, request.Categoria, request.Observacao),
            cancellationToken);

        return gasto is null
            ? NotFound(new { message = "Gasto não encontrado." })
            : Ok(gasto);
    }

    [HttpDelete("{gastoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(
        Guid gastoId,
        [FromServices] IFinanceOperationsService financeOperationsService,
        CancellationToken cancellationToken = default)
    {
        var removido = await financeOperationsService.ExcluirGastoAsync(gastoId, cancellationToken);
        return removido
            ? NoContent()
            : NotFound(new { message = "Gasto não encontrado." });
    }
}
