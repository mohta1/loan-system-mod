using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable
namespace LoanSystem.Modules.IdentityAccess.Infrastructure.Migrations;

[Migration("20260821010000_IdentityAccess_Initial")]
[DbContext(typeof(IdentityAccessDbContext))]
internal sealed class IdentityAccess_Initial : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.EnsureSchema("identity");
        m.CreateTable(name: "permissions", schema: "identity", columns: t => new { Id = t.Column<Guid>(nullable: false), Key = t.Column<string>(maxLength: 150, nullable: false) }, constraints: t => t.PrimaryKey("PK_permissions", x => x.Id));
        m.CreateTable(name: "roles", schema: "identity", columns: t => new { Id = t.Column<Guid>(nullable: false), Name = t.Column<string>(maxLength: 150, nullable: false) }, constraints: t => t.PrimaryKey("PK_roles", x => x.Id));
        m.CreateTable(name: "users", schema: "identity", columns: t => new { Id = t.Column<Guid>(nullable: false), Username = t.Column<string>(maxLength: 100, nullable: false), NormalizedUsername = t.Column<string>(maxLength: 100, nullable: false), DisplayName = t.Column<string>(maxLength: 200, nullable: false), PasswordHash = t.Column<string>(maxLength: 500, nullable: false), IsActive = t.Column<bool>(nullable: false), CreatedAtUtc = t.Column<DateTimeOffset>(nullable: false), UpdatedAtUtc = t.Column<DateTimeOffset>(nullable: false), RowVersion = t.Column<byte[]>(rowVersion: true, nullable: false) }, constraints: t => t.PrimaryKey("PK_users", x => x.Id));
        m.CreateTable(name: "role_permissions", schema: "identity", columns: t => new { RoleId = t.Column<Guid>(nullable: false), PermissionId = t.Column<Guid>(nullable: false) }, constraints: t => { t.PrimaryKey("PK_role_permissions", x => new { x.RoleId, x.PermissionId }); t.ForeignKey("FK_role_permissions_roles_RoleId", x => x.RoleId, "identity", "roles", "Id", onDelete: ReferentialAction.Cascade); t.ForeignKey("FK_role_permissions_permissions_PermissionId", x => x.PermissionId, "identity", "permissions", "Id", onDelete: ReferentialAction.Cascade); });
        m.CreateTable(name: "user_roles", schema: "identity", columns: t => new { UserId = t.Column<Guid>(nullable: false), RoleId = t.Column<Guid>(nullable: false) }, constraints: t => { t.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId }); t.ForeignKey("FK_user_roles_users_UserId", x => x.UserId, "identity", "users", "Id", onDelete: ReferentialAction.Cascade); t.ForeignKey("FK_user_roles_roles_RoleId", x => x.RoleId, "identity", "roles", "Id", onDelete: ReferentialAction.Cascade); });
        m.CreateIndex("IX_permissions_Key", "identity", "permissions", "Key", unique: true); m.CreateIndex("IX_roles_Name", "identity", "roles", "Name", unique: true); m.CreateIndex("IX_users_NormalizedUsername", "identity", "users", "NormalizedUsername", unique: true); m.CreateIndex("IX_role_permissions_PermissionId", "identity", "role_permissions", "PermissionId"); m.CreateIndex("IX_user_roles_RoleId", "identity", "user_roles", "RoleId");
    }
    protected override void Down(MigrationBuilder m) { m.DropTable("role_permissions", "identity"); m.DropTable("user_roles", "identity"); m.DropTable("permissions", "identity"); m.DropTable("roles", "identity"); m.DropTable("users", "identity"); }
}
