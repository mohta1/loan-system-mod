using System.Security.Claims;
using LoanSystem.Modules.IdentityAccess.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.IdentityAccess.Presentation;

internal sealed record LoginRequest(string Username, string Password);
internal sealed record CreateUserRequest(string Username, string DisplayName, string Password);
internal sealed record UpdateUserRequest(string DisplayName);
internal sealed record AssignRolesRequest(IReadOnlyList<Guid> RoleIds);

internal static class IdentityEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        auth.MapPost("/login", Login).AllowAnonymous(); auth.MapPost("/logout", (Delegate)Logout).RequireAuthorization(); auth.MapGet("/me", Me).RequireAuthorization();
        var users = endpoints.MapGroup("/api/v1/users").WithTags("User Administration").RequireAuthorization(IdentityPermissions.ManageUsers);
        users.MapGet("/", List); users.MapPost("/", Create); users.MapGet("/{id:guid}", Get); users.MapPut("/{id:guid}", Update);
        users.MapPost("/{id:guid}/activate", (Guid id, UserAdministration app, CancellationToken ct) => Active(id, true, app, ct));
        users.MapPost("/{id:guid}/deactivate", (Guid id, UserAdministration app, CancellationToken ct) => Active(id, false, app, ct));
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
    private static async Task<IResult> Me(ICurrentUser current, IIdentityStore store, IPermissionChecker checker, CancellationToken ct)
    {
        if (current.UserId is not { } id) return Results.Unauthorized(); var user = await store.FindAsync(id, ct); if (user is not { IsActive: true }) return Results.Unauthorized();
        var permissions = Infrastructure.IdentitySeed.PermissionKeys.Where(x => checker.HasPermissionAsync(id, x, ct).GetAwaiter().GetResult()).ToArray(); return Results.Ok(new CurrentUserDto(id, user.Username, user.DisplayName, permissions));
    }
    private static async Task<IResult> List(IIdentityStore store, CancellationToken ct) => Results.Ok((await store.ListAsync(ct)).Select(UserAdministration.Map));
    private static async Task<IResult> Get(Guid id, IIdentityStore store, CancellationToken ct) { var u = await store.FindAsync(id, ct); return u is null ? Results.NotFound() : WithEtag(UserAdministration.Map(u)); }
    private static async Task<IResult> Create(CreateUserRequest r, UserAdministration app, CancellationToken ct) { var result = await app.CreateAsync(new(r.Username, r.DisplayName, r.Password), ct); return result.User is null ? Problem(result.Error == "identity.usernameConflict" ? 409 : 400, "Unable to create user", result.Error!) : Results.Created($"/api/v1/users/{result.User.UserId}", result.User); }
    private static async Task<IResult> Update(Guid id, UpdateUserRequest r, HttpRequest request, IIdentityStore store, UserAdministration app, CancellationToken ct) { if (!await Matches(id, request, store, ct)) return Precondition(); try { var u = await app.UpdateAsync(id, r.DisplayName, ct); return u is null ? Results.NotFound() : WithEtag(u); } catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return Precondition(); } }
    private static async Task<IResult> Active(Guid id, bool active, UserAdministration app, CancellationToken ct) { var u = await app.SetActiveAsync(id, active, ct); return u is null ? Results.NotFound() : WithEtag(u); }
    private static async Task<IResult> Roles(Guid id, AssignRolesRequest r, HttpRequest request, IIdentityStore store, UserAdministration app, CancellationToken ct) { if (!await Matches(id, request, store, ct)) return Precondition(); try { var u = await app.AssignRolesAsync(id, r.RoleIds, ct); return u is null ? Results.NotFound() : WithEtag(u); } catch (ArgumentException) { return Problem(400, "Unknown role", "identity.unknownRole"); } }
    private static async Task<IResult> RoleCatalog(IIdentityStore store, CancellationToken ct) => Results.Ok((await store.RolesAsync(ct)).Select(x => new RoleDto(x.Id, x.Name)));
    private static async Task<bool> Matches(Guid id, HttpRequest req, IIdentityStore store, CancellationToken ct) { if (!req.Headers.TryGetValue("If-Match", out var value)) return false; var u = await store.FindAsync(id, ct); return u is not null && value.ToString().Trim('"') == UserAdministration.Map(u).ETag; }
    private static IResult WithEtag(UserDto user) => ResultExtensions.WithHeader(Results.Ok(user), "ETag", $"\"{user.ETag}\"");
    private static IResult Precondition() => Problem(412, "The user was changed by another request", "identity.concurrencyConflict");
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
