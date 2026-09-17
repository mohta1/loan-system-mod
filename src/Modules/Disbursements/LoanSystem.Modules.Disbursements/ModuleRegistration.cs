using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LoanSystem.Contracts;
using LoanSystem.Modules.Disbursements.Application;
using LoanSystem.Modules.Disbursements.Infrastructure;
using LoanSystem.Modules.Disbursements.Presentation;

namespace LoanSystem.Modules.Disbursements;

/// <summary>Defines the composition entry points for the Disbursements module.</summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddDisbursementsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration.GetConnectionString("LoanSystem") ?? throw new InvalidOperationException("ConnectionStrings:LoanSystem is required."); services.AddDbContext<DisbursementsDbContext>(x => x.UseSqlServer(connection)); services.AddScoped<IDisbursementStore>(x => x.GetRequiredService<DisbursementsDbContext>()); services.AddScoped<DisbursementService>(); services.AddScoped<IDisbursementCapacityReservedConsumer>(x => x.GetRequiredService<DisbursementService>()); services.AddScoped<IDisbursementCapacityRejectedConsumer>(x => x.GetRequiredService<DisbursementService>()); services.AddHostedService<DisbursementOutboxDispatcher>(); return services;
    }

    public static IEndpointRouteBuilder MapDisbursementsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        DisbursementEndpoints.Map(endpoints); return endpoints;
    }
    public static async Task InitializeDisbursementsAsync(this IServiceProvider services, CancellationToken cancellationToken = default) { using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<DisbursementsDbContext>().Database.MigrateAsync(cancellationToken); }
}
