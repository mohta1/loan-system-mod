using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Contracts;
using LoanSystem.Modules.LoanProducts.Application;
using LoanSystem.Modules.LoanProducts.Infrastructure;
using LoanSystem.Modules.LoanProducts.Presentation;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.LoanProducts;

/// <summary>Defines the composition entry points for the LoanProducts module.</summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddLoanProductsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connection=configuration.GetConnectionString("LoanSystem")??throw new InvalidOperationException("ConnectionStrings:LoanSystem is required.");services.AddDbContext<LoanProductsDbContext>(o=>o.UseSqlServer(connection));services.AddScoped<ILoanProductStore>(x=>x.GetRequiredService<LoanProductsDbContext>());services.AddScoped<ILoanProductsModule,LoanProductsModule>();services.AddSingleton<IBusinessClock,SystemBusinessClock>();services.AddScoped<LoanProductService>();return services;
    }

    public static IEndpointRouteBuilder MapLoanProductsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LoanProductEndpoints.Map(endpoints);return endpoints;
    }
    public static async Task InitializeLoanProductsAsync(this IServiceProvider services,CancellationToken ct=default){using var scope=services.CreateScope();await scope.ServiceProvider.GetRequiredService<LoanProductsDbContext>().Database.MigrateAsync(ct);}
}
