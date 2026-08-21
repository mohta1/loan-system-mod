using LoanSystem.Modules.IdentityAccess;
using LoanSystem.Modules.Borrowers;
using LoanSystem.Modules.LoanProducts;
using LoanSystem.Modules.LoanOrigination;
using LoanSystem.Modules.LoanAccounts;
using LoanSystem.Modules.Disbursements;
using LoanSystem.Modules.Treasury;
using LoanSystem.Modules.Repayments;
using LoanSystem.Modules.Documents;
using LoanSystem.Modules.Audit;
using LoanSystem.Modules.Reporting;
using LoanSystem.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddIdentityAccessModule(builder.Configuration)
    .AddBorrowersModule(builder.Configuration)
    .AddLoanProductsModule(builder.Configuration)
    .AddLoanOriginationModule(builder.Configuration)
    .AddLoanAccountsModule(builder.Configuration)
    .AddDisbursementsModule(builder.Configuration)
    .AddTreasuryModule(builder.Configuration)
    .AddRepaymentsModule(builder.Configuration)
    .AddDocumentsModule(builder.Configuration)
    .AddAuditModule(builder.Configuration)
    .AddReportingModule(builder.Configuration);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PlatformDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("LoanSystem") ?? "Server=localhost,1433;Database=LoanSystem;User Id=sa;Password=Your_password123;TrustServerCertificate=True"));
var healthChecks = builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
if (builder.Environment.IsEnvironment("Testing")) healthChecks.AddCheck("test-ready", () => HealthCheckResult.Healthy(), tags: ["ready"]);
else healthChecks.AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"]);

var app = builder.Build();
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
}
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    await app.Services.InitializeIdentityAccessAsync(app.Configuration, migrate: true);
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    await app.Services.InitializeDocumentsAsync();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    await next();
});
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app
    .MapIdentityAccessModuleEndpoints()
    .MapBorrowersModuleEndpoints()
    .MapLoanProductsModuleEndpoints()
    .MapLoanOriginationModuleEndpoints()
    .MapLoanAccountsModuleEndpoints()
    .MapDisbursementsModuleEndpoints()
    .MapTreasuryModuleEndpoints()
    .MapRepaymentsModuleEndpoints()
    .MapDocumentsModuleEndpoints()
    .MapAuditModuleEndpoints()
    .MapReportingModuleEndpoints();
app.MapHealthChecks("/health/live", new() { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });
app.MapGet("/api/v1/system/info", () => Results.Ok(new SystemInfoResponse("Loan System", "1.0.0", "Operational", DateTimeOffset.UtcNow)))
   .WithName("GetSystemInfo");
app.MapGet("/api/v1/system/error", () => Results.Problem(statusCode: 500, title: "Diagnostic failure"))
   .ExcludeFromDescription();
app.Run();

public sealed record SystemInfoResponse(string Name, string Version, string Status, DateTimeOffset TimestampUtc);
public partial class Program;
