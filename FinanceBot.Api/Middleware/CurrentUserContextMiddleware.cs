using System.Security.Claims;
using FinanceBot.Application.Contracts;

namespace FinanceBot.Api.Middleware;

public sealed class CurrentUserContextMiddleware
{
    private readonly RequestDelegate _next;

    public CurrentUserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserContextAccessor currentUserContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");
            var email = context.User.FindFirstValue(ClaimTypes.Email)
                ?? context.User.FindFirstValue("email");

            if (!Guid.TryParse(userIdValue, out var usuarioId) || string.IsNullOrWhiteSpace(email))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Token inválido." });
                return;
            }

            currentUserContext.SetAuthenticatedUser(usuarioId, email);
        }

        await _next(context);
    }
}
