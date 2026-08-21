using LoanSystem.Modules.IdentityAccess.Application;
using LoanSystem.Modules.IdentityAccess.Domain;
namespace LoanSystem.ApplicationTests;

public sealed class IdentityApplicationTests
{
    private readonly Store store = new(); private readonly UserAdministration service;
    public IdentityApplicationTests() => service = new(store, new Passwords());
    [Fact] public async Task Create_hashes_password_and_maps_user() { var (dto, error) = await service.CreateAsync(new("alice", "Alice", "password-1234"), default); Assert.Null(error); Assert.Equal("alice", dto!.Username); Assert.NotEqual("password-1234", store.Users[0].PasswordHash); Assert.True(dto.IsActive); }
    [Theory]
    [InlineData("", "Name", "password-1234")]
    [InlineData("name", "", "password-1234")]
    [InlineData("name", "Name", "short")]
    public async Task Create_validates_input(string username, string name, string password) { var (dto, error) = await service.CreateAsync(new(username, name, password), default); Assert.Null(dto); Assert.Equal("identity.validation", error); }
    [Fact] public async Task Create_rejects_duplicate_normalized_username() { store.Users.Add(User("Alice")); var result = await service.CreateAsync(new(" alice ", "Other", "password-1234"), default); Assert.Equal("identity.usernameConflict", result.Error); }
    [Fact] public async Task Mutations_handle_missing_user() { Assert.Null(await service.UpdateAsync(Guid.NewGuid(), "Name", default)); Assert.Null(await service.SetActiveAsync(Guid.NewGuid(), false, default)); Assert.Null(await service.AssignRolesAsync(Guid.NewGuid(), [], default)); }
    [Fact] public async Task Updates_assigns_roles_and_disables() { var user = User("bob"); store.Users.Add(user); var role = Guid.NewGuid(); Assert.Equal("Changed", (await service.UpdateAsync(user.Id, "Changed", default))!.DisplayName); Assert.False((await service.SetActiveAsync(user.Id, false, default))!.IsActive); Assert.Single((await service.AssignRolesAsync(user.Id, [role], default))!.RoleIds); Assert.True(store.Saves >= 3); }
    [Fact] public void User_update_validates_and_maps_version() { var user = User("x"); Assert.Throws<ArgumentException>(() => user.Update("", DateTimeOffset.UtcNow)); user.SetPasswordHash("new"); Assert.Equal("new", user.PasswordHash); Assert.NotNull(UserAdministration.Map(user).ETag); }
    [Fact] public void Identity_contract_dtos_expose_values() { var id = Guid.NewGuid(); var role = Guid.NewGuid(); var user = new UserDto(id, "u", "User", true, [role], "etag"); var roleDto = new RoleDto(role, "Role"); var current = new CurrentUserDto(id, "u", "User", [IdentityPermissions.ManageUsers]); var command = new CreateUserCommand("u", "User", "password-1234"); Assert.Equal(id, user.UserId); Assert.True(user.IsActive); Assert.Equal("etag", user.ETag); Assert.Equal("Role", roleDto.Name); Assert.Contains(IdentityPermissions.ManageUsers, current.Permissions); Assert.Equal("password-1234", command.Password); }
    private static User User(string name) => new(Guid.NewGuid(), name, "Display", "hash", DateTimeOffset.UtcNow);
    private sealed class Passwords : IPasswordService { public string Hash(User user, string password) => $"hashed:{password}"; }
    private sealed class Store : IIdentityStore
    {
        public List<User> Users { get; } = []; public int Saves { get; private set; }
        public Task AddAsync(User user, CancellationToken ct) { Users.Add(user); return Task.CompletedTask; }
        public Task<User?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Users.SingleOrDefault(x => x.Id == id)); public Task<User?> FindByUsernameAsync(string normalized, CancellationToken ct) => Task.FromResult(Users.SingleOrDefault(x => x.NormalizedUsername == normalized)); public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<User>>(Users); public Task<IReadOnlyList<Role>> RolesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Role>>([]); public Task ReplaceRolesAsync(User user, IReadOnlyCollection<Guid> ids, CancellationToken ct) { user.UserRoles.Clear(); foreach (var id in ids) user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = id }); return Task.CompletedTask; }
        public Task SaveAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    }
}
