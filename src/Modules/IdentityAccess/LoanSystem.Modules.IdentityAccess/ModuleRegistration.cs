using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.IdentityAccess.Application;
using LoanSystem.Modules.IdentityAccess.Infrastructure;
using LoanSystem.Modules.IdentityAccess.Presentation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.IdentityAccess;

/// <summary>Defines the composition entry points for the IdentityAccess module.</summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddIdentityAccessModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration.GetConnectionString("LoanSystem") ?? throw new InvalidOperationException("ConnectionStrings:LoanSystem is required.");
        services.AddDbContext<IdentityAccessDbContext>(options => options.UseSqlServer(connection));
        services.AddHttpContextAccessor(); services.AddScoped<IIdentityStore>(x => x.GetRequiredService<IdentityAccessDbContext>()); services.AddScoped<IPasswordService, PasswordService>(); services.AddScoped<PasswordService>(); services.AddScoped<IIdentityProvider, LocalIdentityProvider>(); services.AddScoped<ICurrentUser, CurrentUser>(); services.AddScoped<IPermissionChecker, PermissionChecker>(); services.AddScoped<IAccessProfileReader, AccessProfileReader>(); services.AddScoped<UserAdministration>(); services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => { options.Cookie.HttpOnly = true; options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax; options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest; options.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; }; options.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; }; options.Events.OnValidatePrincipal = async c => { var id = c.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value; var checker = c.HttpContext.RequestServices.GetRequiredService<IPermissionChecker>(); if (!Guid.TryParse(id, out var userId) || !await checker.HasPermissionAsync(userId, "__active_probe__") && !await c.HttpContext.RequestServices.GetRequiredService<IdentityAccessDbContext>().Users.AnyAsync(x => x.Id == userId && x.IsActive)) { c.RejectPrincipal(); await c.HttpContext.SignOutAsync(); } }; });
        var authorization = services.AddAuthorizationBuilder().AddPolicy(IdentityPermissions.ManageUsers, policy => policy.AddRequirements(new PermissionRequirement(IdentityPermissions.ManageUsers)));
        foreach (var permission in new[] { "borrowers.read", "borrowers.create", "borrowers.update", "borrowers.manageStatus", "borrowers.import" }) authorization.AddPolicy(permission, policy => policy.AddRequirements(new PermissionRequirement(permission)));
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityAccessModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        IdentityEndpoints.Map(endpoints); return endpoints;
    }

    public static Task InitializeIdentityAccessAsync(this IServiceProvider services, IConfiguration configuration, bool migrate, CancellationToken cancellationToken = default) => IdentitySeed.InitializeAsync(services, configuration, migrate, cancellationToken);
}
