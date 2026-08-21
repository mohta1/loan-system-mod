using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.Modules.LoanOrigination;

/// <summary>Defines the composition entry points for the LoanOrigination module.</summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddLoanOriginationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapLoanOriginationModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }
}
