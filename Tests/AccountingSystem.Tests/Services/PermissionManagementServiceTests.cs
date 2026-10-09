using AccountingSystem.Models;
using AccountingSystem.Services.Permissions;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Tests.Services;

public class PermissionManagementServiceTests : BaseTest
{
	private readonly PermissionManagementService _sut;

	public PermissionManagementServiceTests()
	{
		_sut = new PermissionManagementService(Context);
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task<Role> SeedRoleAsync(
		string code,
		string name = "Test Role",
		bool isActive = true)
	{
		var role = new Role
		{
			Code = code,
			Name = name,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};
		Context.Roles.Add(role);
		await Context.SaveChangesAsync();
		return role;
	}

	private async Task<User> SeedUserAsync(
		int? roleId = null,
		bool isActive = true)
	{
		var user = new User
		{
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			UserType = UserType.Employee,
			RoleId = roleId,
			Status = UserStatus.Approved,
			IsActive = isActive,
			IsPasswordSet = true,
			SecurityStamp = Guid.NewGuid().ToString(),
			CreatedAt = DateTime.UtcNow
		};
		Context.Users.Add(user);
		await Context.SaveChangesAsync();
		return user;
	}

	private async Task<Permission> SeedPermissionAsync(
		string code,
		bool isActive = true)
	{
		var perm = new Permission
		{
			Code = code,
			Name = code,
			Group = "Test",
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};
		Context.Permissions.Add(perm);
		await Context.SaveChangesAsync();
		return perm;
	}

	// =========================================
	// CanManageRoleAsync
	// =========================================

	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 0)]
	[InlineData(-1, 1)]
	[InlineData(1, -1)]
	public async Task CanManageRole_WithInvalidIds_ReturnsFalse(
		int userId, int roleId)
	{
		var result = await _sut.CanManageRoleAsync(userId, roleId);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanManageRole_WhenUserInactive_ReturnsFalse()
	{
		var role = await SeedRoleAsync("Manager");
		var user = await SeedUserAsync(isActive: false);

		var result = await _sut.CanManageRoleAsync(user.Id, role.Id);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanManageRole_AdminCanManageAnyRole_ReturnsTrue()
	{
		var adminRole = await SeedRoleAsync("Admin");
		var targetRole = await SeedRoleAsync("Manager");
		var admin = await SeedUserAsync(roleId: adminRole.Id);

		var result = await _sut.CanManageRoleAsync(admin.Id, targetRole.Id);

		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanManageRole_AdminCannotManageAdminRole_ReturnsFalse()
	{
		var adminRole = await SeedRoleAsync("Admin");
		var admin = await SeedUserAsync(roleId: adminRole.Id);

		var result = await _sut.CanManageRoleAsync(admin.Id, adminRole.Id);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanManageRole_GeneralManagerCanManageRoles_ReturnsTrue()
	{
		var gmRole = await SeedRoleAsync("GeneralManager");
		var targetRole = await SeedRoleAsync("Manager");
		var gm = await SeedUserAsync(roleId: gmRole.Id);

		var result = await _sut.CanManageRoleAsync(gm.Id, targetRole.Id);

		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanManageRole_RegularUserCannotManageRoles_ReturnsFalse()
	{
		var userRole = await SeedRoleAsync("Cashier");
		var targetRole = await SeedRoleAsync("Manager");
		var user = await SeedUserAsync(roleId: userRole.Id);

		var result = await _sut.CanManageRoleAsync(user.Id, targetRole.Id);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanManageRole_WhenTargetRoleInactive_ReturnsFalse()
	{
		var adminRole = await SeedRoleAsync("Admin");
		var targetRole = await SeedRoleAsync("Manager", isActive: false);
		var admin = await SeedUserAsync(roleId: adminRole.Id);

		var result = await _sut.CanManageRoleAsync(admin.Id, targetRole.Id);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanManageRole_WhenUserNotFound_ReturnsFalse()
	{
		var targetRole = await SeedRoleAsync("Manager");

		var result = await _sut.CanManageRoleAsync(99999, targetRole.Id);

		result.Should().BeFalse();
	}

	// =========================================
	// CanAssignPermissionAsync
	// =========================================

	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 0)]
	public async Task CanAssignPermission_WithInvalidIds_ReturnsFalse(
		int userId, int permissionId)
	{
		var result = await _sut.CanAssignPermissionAsync(userId, permissionId);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanAssignPermission_WhenPermissionInactive_ReturnsFalse()
	{
		var adminRole = await SeedRoleAsync("Admin");
		var admin = await SeedUserAsync(roleId: adminRole.Id);
		var perm = await SeedPermissionAsync("test.perm", isActive: false);

		var result = await _sut.CanAssignPermissionAsync(admin.Id, perm.Id);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanAssignPermission_AdminCanAssignAny_ReturnsTrue()
	{
		var adminRole = await SeedRoleAsync("Admin");
		var admin = await SeedUserAsync(roleId: adminRole.Id);
		var perm = await SeedPermissionAsync("test.perm");

		var result = await _sut.CanAssignPermissionAsync(admin.Id, perm.Id);

		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanAssignPermission_UserWithSamePermission_ReturnsTrue()
	{
		var role = await SeedRoleAsync("Manager");
		var user = await SeedUserAsync(roleId: role.Id);
		var perm = await SeedPermissionAsync("test.perm");

		Context.RolePermissions.Add(new RolePermission
		{
			RoleId = role.Id,
			PermissionId = perm.Id,
			IsGranted = true,
			CreatedAt = DateTime.UtcNow
		});
		await Context.SaveChangesAsync();

		var result = await _sut.CanAssignPermissionAsync(user.Id, perm.Id);

		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanAssignPermission_UserWithoutPermission_ReturnsFalse()
	{
		var role = await SeedRoleAsync("Manager");
		var user = await SeedUserAsync(roleId: role.Id);
		var perm = await SeedPermissionAsync("test.perm");

		var result = await _sut.CanAssignPermissionAsync(user.Id, perm.Id);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanAssignPermission_UserWithRevokedPermission_ReturnsFalse()
	{
		var role = await SeedRoleAsync("Manager");
		var user = await SeedUserAsync(roleId: role.Id);
		var perm = await SeedPermissionAsync("test.perm");

		Context.RolePermissions.Add(new RolePermission
		{
			RoleId = role.Id,
			PermissionId = perm.Id,
			IsGranted = false,
			CreatedAt = DateTime.UtcNow
		});
		await Context.SaveChangesAsync();

		var result = await _sut.CanAssignPermissionAsync(user.Id, perm.Id);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanAssignPermission_WhenUserInactive_ReturnsFalse()
	{
		var role = await SeedRoleAsync("Admin");
		var user = await SeedUserAsync(roleId: role.Id, isActive: false);
		var perm = await SeedPermissionAsync("test.perm");

		var result = await _sut.CanAssignPermissionAsync(user.Id, perm.Id);

		result.Should().BeFalse();
	}
}