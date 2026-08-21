using LoanSystem.Modules.IdentityAccess.Domain;

namespace LoanSystem.Modules.IdentityAccess.Application;

public static class IdentityPermissions { public const string ManageUsers = "identity.users.manage"; }
public interface IIdentityProvider { Task<User?> ValidateAsync(string username, string password, CancellationToken cancellationToken); }
public interface ICurrentUser { Guid? UserId { get; } bool IsAuthenticated { get; } }
public interface IPermissionChecker { Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default); }
public interface IIdentityStore
{
    Task<User?> FindAsync(Guid id, CancellationToken ct); Task<User?> FindByUsernameAsync(string normalized, CancellationToken ct);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct); Task<IReadOnlyList<Role>> RolesAsync(CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct); Task ReplaceRolesAsync(User user, IReadOnlyCollection<Guid> roleIds, CancellationToken ct); Task SaveAsync(CancellationToken ct);
}
public interface IPasswordService { string Hash(User user, string password); }
public sealed record UserDto(Guid UserId, string Username, string DisplayName, bool IsActive, IReadOnlyList<Guid> RoleIds, string ETag);
public sealed record RoleDto(Guid RoleId, string Name);
public sealed record CurrentUserDto(Guid UserId, string Username, string DisplayName, IReadOnlyList<string> Permissions);
public sealed record CreateUserCommand(string Username, string DisplayName, string Password);

public sealed class UserAdministration
{
    private readonly IIdentityStore store; private readonly IPasswordService passwords;
    public UserAdministration(IIdentityStore store, IPasswordService passwords) { this.store = store; this.passwords = passwords; }
    public async Task<(UserDto? User, string? Error)> CreateAsync(CreateUserCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrWhiteSpace(command.DisplayName) || command.Password.Length < 12) return (null, "identity.validation");
        if (await store.FindByUsernameAsync(User.Normalize(command.Username), ct) is not null) return (null, "identity.usernameConflict");
        var user = new User(Guid.NewGuid(), command.Username, command.DisplayName, "pending", DateTimeOffset.UtcNow);
        user.SetPasswordHash(passwords.Hash(user, command.Password)); await store.AddAsync(user, ct); await store.SaveAsync(ct); return (Map(user), null);
    }
    public async Task<UserDto?> SetActiveAsync(Guid id, bool active, CancellationToken ct) { var u = await store.FindAsync(id, ct); if (u is null) return null; u.SetActive(active, DateTimeOffset.UtcNow); await store.SaveAsync(ct); return Map(u); }
    public async Task<UserDto?> UpdateAsync(Guid id, string displayName, CancellationToken ct) { var u = await store.FindAsync(id, ct); if (u is null) return null; u.Update(displayName, DateTimeOffset.UtcNow); await store.SaveAsync(ct); return Map(u); }
    public async Task<UserDto?> AssignRolesAsync(Guid id, IReadOnlyCollection<Guid> roles, CancellationToken ct) { var u = await store.FindAsync(id, ct); if (u is null) return null; await store.ReplaceRolesAsync(u, roles, ct); await store.SaveAsync(ct); return Map(u); }
    public static UserDto Map(User u) => new(u.Id, u.Username, u.DisplayName, u.IsActive, u.UserRoles.Select(x => x.RoleId).ToArray(), Convert.ToBase64String(u.RowVersion));
}
