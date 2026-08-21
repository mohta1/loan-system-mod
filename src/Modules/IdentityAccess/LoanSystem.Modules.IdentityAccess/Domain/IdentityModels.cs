#pragma warning disable CA1711
namespace LoanSystem.Modules.IdentityAccess.Domain;

public sealed class User
{
    private User() { }
    public User(Guid id, string username, string displayName, string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
        Id = id; Username = username.Trim(); NormalizedUsername = Normalize(username); DisplayName = displayName.Trim();
        PasswordHash = passwordHash; IsActive = true; CreatedAtUtc = UpdatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public string Username { get; private set; } = "";
    public string NormalizedUsername { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<UserRole> UserRoles { get; } = [];
    public void Update(string displayName, DateTimeOffset now) { if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required."); DisplayName = displayName.Trim(); UpdatedAtUtc = now; }
    public void SetActive(bool active, DateTimeOffset now) { IsActive = active; UpdatedAtUtc = now; }
    public void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
    public void SetPasswordHash(string hash) => PasswordHash = hash;
    public static string Normalize(string username) => username.Trim().ToUpperInvariant();
}

public sealed class Role { public Guid Id { get; set; } public string Name { get; set; } = ""; public ICollection<RolePermission> RolePermissions { get; } = []; public ICollection<UserRole> UserRoles { get; } = []; }
public sealed class Permission { public Guid Id { get; set; } public string Key { get; set; } = ""; public ICollection<RolePermission> RolePermissions { get; } = []; }
public sealed class UserRole { public Guid UserId { get; set; } public User User { get; set; } = null!; public Guid RoleId { get; set; } public Role Role { get; set; } = null!; }
public sealed class RolePermission { public Guid RoleId { get; set; } public Role Role { get; set; } = null!; public Guid PermissionId { get; set; } public Permission Permission { get; set; } = null!; }
