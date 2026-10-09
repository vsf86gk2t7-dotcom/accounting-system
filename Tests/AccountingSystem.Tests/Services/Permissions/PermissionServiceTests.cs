using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services.Permissions;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Services.Permissions;

public class PermissionServiceTests : BaseTest
{
	private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
	private readonly PermissionService _sut;

	public PermissionServiceTests()
	{
		_httpContextAccessorMock = new Mock<IHttpContextAccessor>();
		_httpContextAccessorMock
			.Setup(x => x.HttpContext)
			.Returns((HttpContext?)null); // بدون caching افتراضياً

		_sut = new PermissionService(
			Context,
			_httpContextAccessorMock.Object);
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task<Role> SeedRoleAsync(string code = "TestRole")
	{
		var role = new Role
		{
			Name = code,
			Code = code,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		Context.Roles.Add(role);
		await Context.SaveChangesAsync();

		return role;
	}

	private async Task<Permission> SeedPermissionAsync(
		string code,
		bool isActive = true)
	{
		var permission = new Permission
		{
			Code = code,
			Name = code,
			Group = "Test",
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};

		Context.Permissions.Add(permission);
		await Context.SaveChangesAsync();

		return permission;
	}

	private async Task<User> SeedUserAsync(int? roleId = null)
	{
		var user = new User
		{
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			UserType = UserType.Employee,
			RoleId = roleId,
			Status = UserStatus.Approved,
			IsActive = true,
			IsPasswordSet = true,
			CreatedAt = DateTime.UtcNow
		};

		Context.Users.Add(user);
		await Context.SaveChangesAsync();

		return user;
	}

	private async Task GrantRolePermissionAsync(
		int roleId,
		int permissionId,
		bool isGranted = true)
	{
		Context.RolePermissions.Add(new RolePermission
		{
			RoleId = roleId,
			PermissionId = permissionId,
			IsGranted = isGranted,
			CreatedAt = DateTime.UtcNow
		});

		await Context.SaveChangesAsync();
	}

	private async Task SetUserPermissionAsync(
		int userId,
		int permissionId,
		bool isGranted)
	{
		Context.UserPermissions.Add(new UserPermission
		{
			UserId = userId,
			PermissionId = permissionId,
			IsGranted = isGranted,
			CreatedAt = DateTime.UtcNow
		});

		await Context.SaveChangesAsync();
	}

	// =========================================
	// 1. GetUserPermissionsAsync — حالات الحدود
	// =========================================

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public async Task GetUserPermissionsAsync_WithInvalidUserId_ReturnsEmpty(
		int invalidUserId)
	{
		var result = await _sut.GetUserPermissionsAsync(invalidUserId);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetUserPermissionsAsync_WhenUserNotFound_ReturnsEmpty()
	{
		var result = await _sut.GetUserPermissionsAsync(99999);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetUserPermissionsAsync_WhenUserHasNoRole_ReturnsEmpty()
	{
		var user = await SeedUserAsync(roleId: null);

		var result = await _sut.GetUserPermissionsAsync(user.Id);

		result.Should().BeEmpty();
	}

	// =========================================
	// 2. GetUserPermissionsAsync — من الدور
	// =========================================

	[Fact]
	public async Task GetUserPermissionsAsync_ReturnsRolePermissions()
	{
		var role = await SeedRoleAsync();
		var perm1 = await SeedPermissionAsync("sales.view");
		var perm2 = await SeedPermissionAsync("sales.create");
		var user = await SeedUserAsync(role.Id);

		await GrantRolePermissionAsync(role.Id, perm1.Id);
		await GrantRolePermissionAsync(role.Id, perm2.Id);

		var result = await _sut.GetUserPermissionsAsync(user.Id);

		result.Should().Contain("sales.view");
		result.Should().Contain("sales.create");
		result.Should().HaveCount(2);
	}

	[Fact]
	public async Task GetUserPermissionsAsync_IgnoresDeniedRolePermissions()
	{
		var role = await SeedRoleAsync();
		var perm = await SeedPermissionAsync("sales.view");
		var user = await SeedUserAsync(role.Id);

		await GrantRolePermissionAsync(role.Id, perm.Id, isGranted: false);

		var result = await _sut.GetUserPermissionsAsync(user.Id);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetUserPermissionsAsync_IgnoresInactivePermissions()
	{
		var role = await SeedRoleAsync();
		var activePerm = await SeedPermissionAsync("sales.view", isActive: true);
		var inactivePerm = await SeedPermissionAsync("sales.delete", isActive: false);
		var user = await SeedUserAsync(role.Id);

		await GrantRolePermissionAsync(role.Id, activePerm.Id);
		await GrantRolePermissionAsync(role.Id, inactivePerm.Id);

		var result = await _sut.GetUserPermissionsAsync(user.Id);

		result.Should().Contain("sales.view");
		result.Should().NotContain("sales.delete");
	}

	// =========================================
	// 3. GetUserPermissionsAsync — الصلاحيات المباشرة
	// =========================================

	[Fact]
	public async Task GetUserPermissionsAsync_AddsDirectlyGrantedPermission()
	{
		var perm = await SeedPermissionAsync("special.access");
		var user = await SeedUserAsync();

		await SetUserPermissionAsync(user.Id, perm.Id, isGranted: true);

		var result = await _sut.GetUserPermissionsAsync(user.Id);

		result.Should().Contain("special.access");
	}

	[Fact]
	public async Task GetUserPermissionsAsync_DeniedOverridesRoleGrant()
	{
		// السيناريو: المستخدم عنده الصلاحية من الدور، لكن
		// تم منعه مباشرة → النتيجة النهائية: ممنوع
		var role = await SeedRoleAsync();
		var perm = await SeedPermissionAsync("sales.delete");
		var user = await SeedUserAsync(role.Id);

		await GrantRolePermissionAsync(role.Id, perm.Id, isGranted: true);
		await SetUserPermissionAsync(user.Id, perm.Id, isGranted: false);

		var result = await _sut.GetUserPermissionsAsync(user.Id);

		result.Should().NotContain("sales.delete");
	}

	[Fact]
	public async Task GetUserPermissionsAsync_GrantedDirectOverridesNothing()
	{
		// المستخدم عنده الصلاحية من الدور، وتم تأكيدها مباشرة
		var role = await SeedRoleAsync();
		var perm = await SeedPermissionAsync("sales.view");
		var user = await SeedUserAsync(role.Id);

		await GrantRolePermissionAsync(role.Id, perm.Id, isGranted: true);
		await SetUserPermissionAsync(user.Id, perm.Id, isGranted: true);

		var result = await _sut.GetUserPermissionsAsync(user.Id);

		result.Should().Contain("sales.view");
		result.Should().HaveCount(1);
	}

	// =========================================
	// 4. HasPermissionAsync
	// =========================================

	[Theory]
	[InlineData(0, "sales.view")]
	[InlineData(-1, "sales.view")]
	[InlineData(1, "")]
	[InlineData(1, "   ")]
	[InlineData(1, null)]
	public async Task HasPermissionAsync_WithInvalidInput_ReturnsFalse(
		int userId,
		string? code)
	{
		var result = await _sut.HasPermissionAsync(userId, code!);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task HasPermissionAsync_WithGrantedPermission_ReturnsTrue()
	{
		var role = await SeedRoleAsync();
		var perm = await SeedPermissionAsync("sales.view");
		var user = await SeedUserAsync(role.Id);

		await GrantRolePermissionAsync(role.Id, perm.Id);

		var result = await _sut.HasPermissionAsync(user.Id, "sales.view");

		result.Should().BeTrue();
	}

	[Fact]
	public async Task HasPermissionAsync_WithNonGrantedPermission_ReturnsFalse()
	{
		var user = await SeedUserAsync();

		var result = await _sut.HasPermissionAsync(user.Id, "sales.view");

		result.Should().BeFalse();
	}

	[Fact]
	public async Task HasPermissionAsync_IsCaseInsensitive()
	{
		var role = await SeedRoleAsync();
		var perm = await SeedPermissionAsync("Sales.View");
		var user = await SeedUserAsync(role.Id);

		await GrantRolePermissionAsync(role.Id, perm.Id);

		var result = await _sut.HasPermissionAsync(user.Id, "SALES.VIEW");

		result.Should().BeTrue();
	}

	[Fact]
	public async Task HasPermissionAsync_TrimsWhitespace()
	{
		var role = await SeedRoleAsync();
		var perm = await SeedPermissionAsync("sales.view");
		var user = await SeedUserAsync(role.Id);

		await GrantRolePermissionAsync(role.Id, perm.Id);

		var result = await _sut.HasPermissionAsync(user.Id, "  sales.view  ");

		result.Should().BeTrue();
	}

	// =========================================
	// 5. Caching (مع HttpContext موجود)
	// =========================================

	[Fact]
	public async Task HasPermissionAsync_WhenHttpContextExists_CachesPermissions()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		_httpContextAccessorMock
			.Setup(x => x.HttpContext)
			.Returns(httpContext);

		var role = await SeedRoleAsync();
		var perm = await SeedPermissionAsync("sales.view");
		var user = await SeedUserAsync(role.Id);
		await GrantRolePermissionAsync(role.Id, perm.Id);

		var sut = new PermissionService(
			Context,
			_httpContextAccessorMock.Object);

		// Act — نداء أول
		var result1 = await sut.HasPermissionAsync(user.Id, "sales.view");

		// حذف الصلاحية من الـ DB
		var rp = await Context.RolePermissions.FirstAsync();
		Context.RolePermissions.Remove(rp);
		await Context.SaveChangesAsync();

		// نداء تاني — المفروض يستخدم الـ cache
		var result2 = await sut.HasPermissionAsync(user.Id, "sales.view");

		// Assert
		result1.Should().BeTrue();
		result2.Should().BeTrue(); // من الـ cache
	}

	[Fact]
	public async Task GetUserPermissionsAsync_WithHttpContext_ReturnsConsistentResults()
	{
		var httpContext = new DefaultHttpContext();
		_httpContextAccessorMock
			.Setup(x => x.HttpContext)
			.Returns(httpContext);

		var role = await SeedRoleAsync();
		var perm = await SeedPermissionAsync("sales.view");
		var user = await SeedUserAsync(role.Id);
		await GrantRolePermissionAsync(role.Id, perm.Id);

		var sut = new PermissionService(
			Context,
			_httpContextAccessorMock.Object);

		var result1 = await sut.GetUserPermissionsAsync(user.Id);
		var result2 = await sut.GetUserPermissionsAsync(user.Id);

		result1.Should().BeEquivalentTo(result2);
		result1.Should().Contain("sales.view");
	}
}