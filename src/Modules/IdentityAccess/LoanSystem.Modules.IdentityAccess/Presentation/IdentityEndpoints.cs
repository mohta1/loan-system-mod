using System.Security.Claims;
using LoanSystem.Modules.IdentityAccess.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.IdentityAccess.Presentation;

internal sealed record LoginRequest(string? Username, string? Password);
internal sealed record CreateUserRequest(string? Username, string? DisplayName, string? Password, IReadOnlyList<Guid>? RoleIds);
internal sealed record UpdateUserRequest(string? DisplayName);
internal sealed record AssignRolesRequest(IReadOnlyList<Guid>? RoleIds);

internal static class IdentityEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        auth.MapPost("/login", Login).AllowAnonymous(); auth.MapPost("/logout", (Delegate)Logout).RequireAuthorization(); auth.MapGet("/me", Me).RequireAuthorization();
        var users = endpoints.MapGroup("/api/v1/users").WithTags("User Administration").RequireAuthorization(IdentityPermissions.ManageUsers);
        users.MapGet("/", List); users.MapPost("/", Create); users.MapGet("/{id:guid}", Get); users.MapPut("/{id:guid}", Update);
        users.MapPost("/{id:guid}/activate", (Guid id, HttpRequest request, UserAdministration app, CancellationToken ct) => Active(id, true, request, app, ct));
        users.MapPost("/{id:guid}/deactivate", (Guid id, HttpRequest request, UserAdministration app, CancellationToken ct) => Active(id, false, request, app, ct));
        users.MapPut("/{id:guid}/roles", Roles);
        endpoints.MapGet("/api/v1/roles", RoleCatalog).RequireAuthorization(IdentityPermissions.ManageUsers).WithTags("User Administration");
    }
    private static async Task<IResult> Login(LoginRequest request, IIdentityProvider provider, HttpContext context, CancellationToken ct)
    {
        var user = await provider.ValidateAsync(request.Username, request.Password, ct); if (user is null) return Problem(401, "Invalid username or password", "identity.invalidCredentials");
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username)], CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity)); return Results.NoContent();
    }
    private static async Task<IResult> Logout(HttpContext context) { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.NoContent(); }
    private static async Task<IResult> Me(ICurrentUser current, IAccessProfileReader profiles, CancellationToken ct)
    {
        if (current.UserId is not { } id) return Results.Unauthorized(); var profile = await profiles.GetAsync(id, ct); return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }
    private static async Task<IResult> List(IIdentityStore store, CancellationToken ct) => Results.Ok((await store.ListAsync(ct)).Select(UserAdministration.Map));
    private static async Task<IResult> Get(Guid id, IIdentityStore store, CancellationToken ct) { var u = await store.FindAsync(id, ct); return u is null ? Results.NotFound() : WithEtag(UserAdministration.Map(u)); }
    private static async Task<IResult> Create(CreateUserRequest r, UserAdministration app, CancellationToken ct) { var result = await app.CreateAsync(new(r.Username, r.DisplayName, r.Password, r.RoleIds), ct); return result.User is null ? Problem(result.Error == "identity.usernameConflict" ? 409 : 400, "Unable to create user", result.Error!) : Results.Created($"/api/v1/users/{result.User.UserId}", result.User); }
    private static async Task<IResult> Update(Guid id, UpdateUserRequest r, HttpRequest request, UserAdministration app, CancellationToken ct) { if (!TryVersion(request, out var version)) return Precondition(); try { var u = await app.UpdateAsync(id, r.DisplayName, version, ct); return u is null ? Results.NotFound() : WithEtag(u); } catch (ArgumentException) { return Problem(400, "Invalid user", "identity.validation"); } catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return Precondition(); } }
    private static async Task<IResult> Active(Guid id, bool active, HttpRequest request, UserAdministration app, CancellationToken ct) { if (!TryVersion(request, out var version)) return Precondition(); try { var u = await app.SetActiveAsync(id, active, version, ct); return u is null ? Results.NotFound() : WithEtag(u); } catch (LastAdministratorRequiredException) { return LastAdministrator(); } catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return Precondition(); } }
    private static async Task<IResult> Roles(Guid id, AssignRolesRequest r, HttpRequest request, UserAdministration app, CancellationToken ct) { if (!TryVersion(request, out var version)) return Precondition(); try { var u = await app.AssignRolesAsync(id, r.RoleIds, version, ct); return u is null ? Results.NotFound() : WithEtag(u); } catch (LastAdministratorRequiredException) { return LastAdministrator(); } catch (ArgumentException ex) { return Problem(400, ex.Message == "Roles are required." ? "Roles are required" : "Unknown role", ex.Message == "Roles are required." ? "identity.validation" : "identity.unknownRole"); } catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return Precondition(); } }
    private static async Task<IResult> RoleCatalog(IIdentityStore store, CancellationToken ct) => Results.Ok((await store.RolesAsync(ct)).Select(x => new RoleDto(x.Id, x.Name)));
    private static bool TryVersion(HttpRequest request, out byte[] version) { version = []; if (!request.Headers.TryGetValue("If-Match", out var value)) return false; try { version = Convert.FromBase64String(value.ToString().Trim('"')); return true; } catch (FormatException) { return false; } }
    private static IResult WithEtag(UserDto user) => ResultExtensions.WithHeader(Results.Ok(user), "ETag", $"\"{user.ETag}\"");
    private static IResult Precondition() => Problem(412, "The user was changed by another request", "identity.concurrencyConflict");
    private static IResult LastAdministrator() => Problem(409, "At least one active user administrator is required", "identity.lastAdministratorRequired");
    private static IResult Problem(int status, string title, string code) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["errorCode"] = code });
}

internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement { public string Permission { get; } = permission; }
internal sealed class PermissionHandler(IPermissionChecker checker) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement) { var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier); if (Guid.TryParse(value, out var id) && await checker.HasPermissionAsync(id, requirement.Permission)) context.Succeed(requirement); }
}

internal static class ResultExtensions
{
    public static IResult WithHeader(this IResult result, string name, string value) => new HeaderResult(result, name, value);
    private sealed class HeaderResult(IResult inner, string name, string value) : IResult { public Task ExecuteAsync(HttpContext context) { context.Response.Headers[name] = value; return inner.ExecuteAsync(context); } }
}
