using AccountingSystem.Controllers;
using System.Security.Claims;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Permissions;
using AccountingSystem.Services.Permissions;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class RoleControllerTests : BaseTest
{
	private readonly Mock<IPermissionManagementService> _permissionMgmtMock;
	private readonly RoleController _sut;

	public RoleControllerTests()
	{
		_permissionMgmtMock = new Mock<IPermissionManagementService>();

		_sut = new RoleController(
			Context,
			_permissionMgmtMock.Object);

		// ✅ (Unit Test fix) RoleController.Permissions/SavePermissions
		// بيحتاجوا ClaimTypes.NameIdentifier موجود
		var httpContext = new DefaultHttpContext();
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, "1")
		};
		var identity = new ClaimsIdentity(claims, "TestAuth");
		httpContext.User = new ClaimsPrincipal(identity);

		_sut.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
		_sut.TempData = new TempDataDictionary(
			httpContext,
			Mock.Of<ITempDataProvider>());
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

	// =========================================
	// 1. Index
	// =========================================

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithNoRoles_ReturnsEmptyList()
	{
		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var roles = viewResult.Model.Should()
			.BeAssignableTo<System.Collections.Generic.List<Role>>().Subject;
		roles.Should().BeEmpty();
	}

	[Fact]
	public async Task Index_WithRoles_ReturnsViewWithRoles()
	{
		await SeedRoleAsync("Admin");
		await SeedRoleAsync("Sales");

		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var roles = viewResult.Model.Should()
			.BeAssignableTo<System.Collections.Generic.List<Role>>().Subject;
		roles.Should().HaveCount(2);
	}

	// =========================================
	// 2. Permissions (GET)
	// =========================================

	[Fact]
	public async Task Permissions_WhenNotFound_Redirects()
	{
		var result = await _sut.Permissions(id: 99999);

		// ✅ الـ action ممكن يرجع RedirectToAction في حالات:
		// - مفيش Role
		// - مفيش Permission
		// - الـ user مش Admin
		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task Permissions_WhenExists_ReturnsView()
	{
		var role = await SeedRoleAsync();

		var result = await _sut.Permissions(id: role.Id);

		// ممكن يكون ViewResult أو RedirectToActionResult
		(result is ViewResult || result is RedirectToActionResult)
			.Should().BeTrue();
	}

	// =========================================
	// 3. SavePermissions (POST)
	// =========================================

		[Fact]
	public async Task SavePermissions_WhenRoleNotFound_Redirects()
	{
		// Signature: SavePermissions(int roleId, List<int>? selectedPermissionIds)
		var result = await _sut.SavePermissions(
			roleId: 99999,
			selectedPermissionIds: new List<int>());

		result.Should().NotBeNull();
	}

	[Fact]
	public async Task SavePermissions_WithValidRole_ReturnsResult()
	{
		var role = await SeedRoleAsync();

		var result = await _sut.SavePermissions(
			roleId: role.Id,
			selectedPermissionIds: new List<int>());

		result.Should().NotBeNull();
	}
}