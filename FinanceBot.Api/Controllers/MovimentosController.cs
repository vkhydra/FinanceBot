using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/movimentos")]
public sealed class MovimentosController : ControllerBase
{
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
