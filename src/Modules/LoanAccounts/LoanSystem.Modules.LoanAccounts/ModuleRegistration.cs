using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LoanSystem.Contracts;
using LoanSystem.Modules.LoanAccounts.Application;
using LoanSystem.Modules.LoanAccounts.Infrastructure;
using LoanSystem.Modules.LoanAccounts.Presentation;

namespace LoanSystem.Modules.LoanAccounts;

/// <summary>Defines the composition entry points for the LoanAccounts module.</summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddLoanAccountsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration.GetConnectionString("LoanSystem") ?? throw new InvalidOperationException("ConnectionStrings:LoanSystem is required.");
        services.AddDbContext<LoanAccountsDbContext>(options => options.UseSqlServer(connection));
        services.AddScoped<ILoanAccountStore>(x => x.GetRequiredService<LoanAccountsDbContext>());
        services.AddScoped<LoanAccountService>(); services.AddScoped<ILoanApplicationApprovedConsumer>(x => x.GetRequiredService<LoanAccountService>());
        return services;
    }

    public static IEndpointRouteBuilder MapLoanAccountsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        LoanAccountEndpoints.Map(endpoints); return endpoints;
    }

    public static async Task InitializeLoanAccountsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    { using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<LoanAccountsDbContext>().Database.MigrateAsync(cancellationToken); }
}
