using FinanceBot.Api.Requests;
using FinanceBot.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/receitas")]
public sealed class ReceitasController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReceitaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReceitaDto>>> Listar(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromQuery] int limite = 20,
        CancellationToken cancellationToken = default)
    {
        var receitas = await financeOperationsService.ListarReceitasAsync(limite, cancellationToken);
        return Ok(receitas);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReceitaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReceitaDto>> Criar(
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromBody] CriarReceitaHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var receita = await financeOperationsService.RegistrarReceitaAsync(
            new CriarReceitaRequest(request.Descricao, request.Valor, request.EhFixo, request.Observacao),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, receita);
    }

    [HttpPut("{receitaId:guid}")]
    [ProducesResponseType(typeof(ReceitaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceitaDto>> Atualizar(
        Guid receitaId,
        [FromServices] IFinanceOperationsService financeOperationsService,
        [FromBody] AtualizarReceitaHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var receita = await financeOperationsService.AtualizarReceitaAsync(
            receitaId,
            new AtualizarReceitaRequest(request.Descricao, request.Valor, request.Data, request.EhFixo, request.Observacao),
            cancellationToken);

        return receita is null
            ? NotFound(new { message = "Receita não encontrada." })
            : Ok(receita);
    }

    [HttpDelete("{receitaId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(
        Guid receitaId,
        [FromServices] IFinanceOperationsService financeOperationsService,
        CancellationToken cancellationToken = default)
    {
        var removida = await financeOperationsService.ExcluirReceitaAsync(receitaId, cancellationToken);
        return removida
            ? NoContent()
            : NotFound(new { message = "Receita não encontrada." });
    }
}
