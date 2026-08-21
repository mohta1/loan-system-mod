using System.Security.Claims;
using LoanSystem.Modules.IdentityAccess.Application;
using LoanSystem.Modules.IdentityAccess.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.Modules.IdentityAccess.Infrastructure;

internal sealed class IdentityAccessDbContext(DbContextOptions<IdentityAccessDbContext> options) : DbContext(options), IIdentityStore
{
    public DbSet<User> Users => Set<User>(); public DbSet<Role> Roles => Set<Role>(); public DbSet<Permission> Permissions => Set<Permission>();
    protected override void OnModelCreating(ModelBuilder b) { b.ApplyConfigurationsFromAssembly(typeof(IdentityAccessDbContext).Assembly); }
    public Task<User?> FindAsync(Guid id, CancellationToken ct) => Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<User?> FindByUsernameAsync(string normalized, CancellationToken ct) => Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.NormalizedUsername == normalized, ct);
    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) => await Users.AsNoTracking().Include(x => x.UserRoles).OrderBy(x => x.Username).ToListAsync(ct);
    public async Task<IReadOnlyList<Role>> RolesAsync(CancellationToken ct) => await Roles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    public Task AddAsync(User user, CancellationToken ct) { Users.Add(user); return Task.CompletedTask; }
    public async Task ReplaceRolesAsync(User user, IReadOnlyCollection<Guid> ids, CancellationToken ct) { var valid = await Roles.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct); if (valid.Count != ids.Distinct().Count()) throw new ArgumentException("Unknown role."); Set<UserRole>().RemoveRange(user.UserRoles); foreach (var id in valid) user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = id }); }
    public Task SaveAsync(CancellationToken ct) => SaveChangesAsync(ct);
}
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b) { b.ToTable("users", "identity"); b.HasKey(x => x.Id); b.Property(x => x.Username).HasMaxLength(100).IsRequired(); b.Property(x => x.NormalizedUsername).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.NormalizedUsername).IsUnique(); b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired(); b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired(); b.Property(x => x.RowVersion).IsRowVersion(); }
}
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role> { public void Configure(EntityTypeBuilder<Role> b) { b.ToTable("roles", "identity"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(150); b.HasIndex(x => x.Name).IsUnique(); } }
internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission> { public void Configure(EntityTypeBuilder<Permission> b) { b.ToTable("permissions", "identity"); b.HasKey(x => x.Id); b.Property(x => x.Key).HasMaxLength(150); b.HasIndex(x => x.Key).IsUnique(); } }
internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole> { public void Configure(EntityTypeBuilder<UserRole> b) { b.ToTable("user_roles", "identity"); b.HasKey(x => new { x.UserId, x.RoleId }); b.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId); b.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId); } }
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission> { public void Configure(EntityTypeBuilder<RolePermission> b) { b.ToTable("role_permissions", "identity"); b.HasKey(x => new { x.RoleId, x.PermissionId }); b.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId); b.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId); } }

internal sealed class PasswordService : IPasswordService { private readonly PasswordHasher<User> hasher = new(); public string Hash(User user, string password) => hasher.HashPassword(user, password); public PasswordVerificationResult Verify(User user, string password) => hasher.VerifyHashedPassword(user, user.PasswordHash, password); }
internal sealed class LocalIdentityProvider(IdentityAccessDbContext db, PasswordService passwords) : IIdentityProvider
{
    public async Task<User?> ValidateAsync(string username, string password, CancellationToken ct) { var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedUsername == User.Normalize(username), ct); return user is { IsActive: true } && passwords.Verify(user, password) != PasswordVerificationResult.Failed ? user : null; }
}
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser { public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null; public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true; }
internal sealed class PermissionChecker(IdentityAccessDbContext db) : IPermissionChecker
{
    public Task<bool> HasPermissionAsync(Guid id, string permission, CancellationToken ct = default) => db.Users.AsNoTracking().Where(x => x.Id == id && x.IsActive).SelectMany(x => x.UserRoles).SelectMany(x => x.Role.RolePermissions).AnyAsync(x => x.Permission.Key == permission, ct);
}

internal static class IdentitySeed
{
    internal static readonly string[] PermissionKeys = ["identity.users.manage", "borrowers.read", "borrowers.create", "borrowers.update", "borrowers.manageStatus", "borrowers.import", "loanProducts.read", "loanProducts.manage", "loanProducts.publish", "loanProducts.manageStatus", "loanApplications.read", "loanApplications.create", "loanApplications.update", "loanApplications.evaluateEligibility", "loanApplications.submit", "loanApplications.unitApprove", "loanApplications.committeeApprove", "loanApplications.mortgageManage", "loanApplications.finalApprove", "loanApplications.cancel", "inspections.create", "inspections.approve", "loans.read", "loans.close", "disbursements.read", "disbursements.create", "disbursements.technicalApprove", "disbursements.accountingApprove", "disbursements.finalApprove", "disbursements.cancel", "treasury.read", "treasury.input", "treasury.audit", "treasury.approve", "treasury.execute", "repayments.read", "repayments.create", "repayments.import", "audit.read", "reports.read"];
    internal static readonly string[] Roles = ["System Administrator", "Loan Management Officer", "Unit Officer", "Loan Committee Member", "Property Inspector / Technical Affairs Officer", "Accounting Officer", "Higher Administrative Approver", "Treasury Input User", "Treasury Auditor", "Treasury Approver", "Reporting / Audit Viewer"];
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, bool migrate, CancellationToken ct = default)
    {
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>(); if (migrate) await db.Database.MigrateAsync(ct);
        if (!await db.Permissions.AnyAsync(ct)) { db.Permissions.AddRange(PermissionKeys.Select((x, i) => new Permission { Id = Stable("permission:" + i), Key = x })); db.Roles.AddRange(Roles.Select((x, i) => new Role { Id = Stable("role:" + i), Name = x })); await db.SaveChangesAsync(ct); var admin = await db.Roles.SingleAsync(x => x.Name == Roles[0], ct); db.Set<RolePermission>().AddRange(await db.Permissions.Select(x => new RolePermission { RoleId = admin.Id, PermissionId = x.Id }).ToListAsync(ct)); await db.SaveChangesAsync(ct); }
        var username = config["DevelopmentAdmin:Username"]; var password = config["DevelopmentAdmin:Password"]; if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password) && await db.Users.AllAsync(x => x.NormalizedUsername != User.Normalize(username), ct)) { var p = scope.ServiceProvider.GetRequiredService<IPasswordService>(); var user = new User(Guid.NewGuid(), username, config["DevelopmentAdmin:DisplayName"] ?? "Development Administrator", "pending", DateTimeOffset.UtcNow); user.SetPasswordHash(p.Hash(user, password)); var adminRole = await db.Roles.SingleAsync(x => x.Name == Roles[0], ct); user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id }); db.Users.Add(user); await db.SaveChangesAsync(ct); }
    }
    private static Guid Stable(string value) { var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)); return new Guid(bytes[..16]); }
}
