using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/movimentos")]
public sealed class MovimentosController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MovimentoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MovimentoDto>>> Listar(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromQuery] DateOnly? inicio = null,
        [FromQuery] DateOnly? fim = null,
        [FromQuery] string? tipo = null,
        [FromQuery] string? busca = null,
        [FromQuery] string? categoria = null,
        [FromQuery] string? origem = null,
        [FromQuery] int limite = 100,
        CancellationToken cancellationToken = default)
    {
        if (inicio.HasValue && fim.HasValue && inicio.Value > fim.Value)
        {
            return BadRequest(new { message = "O período informado é inválido." });
        }

        if (limite is < 1 or > 200)
        {
            return BadRequest(new { message = "O parâmetro limite deve estar entre 1 e 200." });
        }

        var movimentos = await financeOperationsService.ListarMovimentosAsync(
            new ListarMovimentosRequest(inicio, fim, tipo, busca, categoria, origem, limite),
            cancellationToken);

        return Ok(movimentos);
    }

    [HttpGet("ultimos")]
    [ProducesResponseType(typeof(IReadOnlyList<MovimentoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimentoDto>>> ListarUltimos(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromQuery] int limite = 5,
        CancellationToken cancellationToken = default)
    {
        var movimentos = await financeOperationsService.ListarUltimosMovimentosAsync(limite, cancellationToken);
        return Ok(movimentos);
    }
}
