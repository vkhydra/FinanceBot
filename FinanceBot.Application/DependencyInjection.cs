using FinanceBot.Application.Contracts;
using FinanceBot.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceBot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CurrentUserContext>();
        services.AddScoped<ICurrentUserContext>(serviceProvider => serviceProvider.GetRequiredService<CurrentUserContext>());
        services.AddScoped<ICurrentUserContextAccessor>(serviceProvider => serviceProvider.GetRequiredService<CurrentUserContext>());
        services.AddScoped<IAccessPolicyService, AccessPolicyService>();
        services.AddScoped<IFinanceMessageProcessor, FinanceMessageProcessor>();
        services.AddScoped<IFinanceOperationsService, FinanceOperationsService>();
        services.AddScoped<IIdentityService, IdentityService>();
        return services;
    }
}
