using Erp.Application.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IExternalAuthenticationService, ExternalAuthenticationService>();

        return services;
    }
}
