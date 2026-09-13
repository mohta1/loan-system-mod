using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.LoanOrigination.Application;
using LoanSystem.Modules.LoanOrigination.Infrastructure;
using LoanSystem.Modules.LoanOrigination.Presentation;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.LoanOrigination;

/// <summary>Defines the composition entry points for the LoanOrigination module.</summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddLoanOriginationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration.GetConnectionString("LoanSystem") ?? throw new InvalidOperationException("ConnectionStrings:LoanSystem is required.");
        services.AddDbContext<LoanOriginationDbContext>(options => options.UseSqlServer(connection));
        services.AddScoped<ILoanApplicationStore>(provider => provider.GetRequiredService<LoanOriginationDbContext>());
        services.AddScoped<IPropertyInspectionStore>(provider => provider.GetRequiredService<LoanOriginationDbContext>());
        services.AddScoped<LoanApplicationService>();
        services.AddScoped<PropertyInspectionService>();
        return services;
    }

    public static IEndpointRouteBuilder MapLoanOriginationModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        LoanApplicationEndpoints.Map(endpoints); InspectionEndpoints.Map(endpoints); return endpoints;
    }

    public static async Task InitializeLoanOriginationAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    { using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<LoanOriginationDbContext>().Database.MigrateAsync(cancellationToken); }
}
