using FinanceBot.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceBot.Endpoints;

public static class FinanceEndpoints
{
        public static void MapFinanceEntries(this IEndpointRouteBuilder app)
        {
                var group = app.MapGroup("/whatsapp");

                group.MapPost("/", async (HttpContext context, FinanceService financeService) =>
                {
                        var form = await context.Request.ReadFormAsync();
                        string corpoMensagem = form["Body"];

                        var resultado = await financeService.ProcessarMensagem(corpoMensagem);

                        string mensagemEmoji = resultado.Sucesso ? "✅" : "❌";

                        string respostaTwiML = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                <Response>
                    <Message>{mensagemEmoji} {resultado.Mensagem}</Message>
                </Response>";

                        return Results.Content(respostaTwiML, "application/xml");
                })
                .DisableAntiforgery();

                // No futuro, você pode adicionar group.MapGet("/relatorio", ...) aqui
        }
}