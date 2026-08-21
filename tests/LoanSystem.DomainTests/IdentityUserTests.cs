using LoanSystem.Modules.IdentityAccess.Domain;
namespace LoanSystem.DomainTests;

public sealed class IdentityUserTests
{
    [Fact] public void User_normalizes_and_can_be_disabled() { var now = DateTimeOffset.UtcNow; var user = new User(Guid.NewGuid(), " Alice ", "Alice", "hash", now); Assert.Equal("ALICE", user.NormalizedUsername); Assert.True(user.IsActive); user.SetActive(false, now.AddMinutes(1)); Assert.False(user.IsActive); }
    [Fact] public void User_requires_names() { Assert.Throws<ArgumentException>(() => new User(Guid.NewGuid(), "", "Name", "hash", DateTimeOffset.UtcNow)); Assert.Throws<ArgumentException>(() => new User(Guid.NewGuid(), "name", "", "hash", DateTimeOffset.UtcNow)); }
    [Fact] public void Relationships_expose_persistence_state() { var role = new Role { Id = Guid.NewGuid(), Name = "Reader" }; var permission = new Permission { Id = Guid.NewGuid(), Key = "borrowers.read" }; var user = new User(Guid.NewGuid(), "user", "User", "hash", DateTimeOffset.UtcNow); var userRole = new UserRole { UserId = user.Id, User = user, RoleId = role.Id, Role = role }; var link = new RolePermission { RoleId = role.Id, Role = role, PermissionId = permission.Id, Permission = permission }; role.UserRoles.Add(userRole); role.RolePermissions.Add(link); permission.RolePermissions.Add(link); Assert.Equal("Reader", userRole.Role.Name); Assert.Equal("borrowers.read", link.Permission.Key); Assert.Single(role.UserRoles); Assert.Single(permission.RolePermissions); }
}
