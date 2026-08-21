using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.Modules.Disbursements;

/// <summary>Defines the composition entry points for the Disbursements module.</summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddDisbursementsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapDisbursementsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }
}
