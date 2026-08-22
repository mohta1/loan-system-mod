using LoanSystem.Modules.Borrowers.Application;
using LoanSystem.Modules.Borrowers.Infrastructure;
using LoanSystem.Modules.Borrowers.Presentation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace LoanSystem.Modules.Borrowers;

public static class ModuleRegistration
{ public static IServiceCollection AddBorrowersModule(this IServiceCollection services, IConfiguration configuration) { var connection = configuration.GetConnectionString("LoanSystem") ?? throw new InvalidOperationException("ConnectionStrings:LoanSystem is required."); services.Configure<BorrowerImportOptions>(configuration.GetSection("BorrowerImport")); services.AddDbContext<BorrowersDbContext>(o => o.UseSqlServer(connection)); services.AddScoped<IBorrowerStore>(x => x.GetRequiredService<BorrowersDbContext>()); services.AddScoped<IBorrowerImportStore, BorrowerImportStore>(); services.AddScoped<IBorrowerWorkbookParser, OpenXmlBorrowerWorkbookParser>(); services.AddScoped<BorrowerService>(); services.AddScoped<BorrowerImportService>(); return services; } public static IEndpointRouteBuilder MapBorrowersModuleEndpoints(this IEndpointRouteBuilder endpoints) { BorrowerEndpoints.Map(endpoints); return endpoints; } public static async Task InitializeBorrowersAsync(this IServiceProvider services, CancellationToken ct = default) { using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<BorrowersDbContext>().Database.MigrateAsync(ct); } }
