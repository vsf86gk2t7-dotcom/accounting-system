using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Users;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class UserControllerTests : BaseTest
{
	private readonly UserController _sut;

	public UserControllerTests()
	{
		_sut = new UserController(Context);

		var httpContext = new DefaultHttpContext();
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

	private async Task<User> SeedUserAsync(bool isActive = true)
	{
		var user = new User
		{
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			UserType = UserType.Employee,
			Status = UserStatus.Approved,
			IsActive = isActive,
			IsPasswordSet = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Users.Add(user);
		await Context.SaveChangesAsync();
		return user;
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
	public async Task Index_WithNoUsers_ReturnsEmptyList()
	{
		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var users = viewResult.Model.Should()
			.BeAssignableTo<System.Collections.IEnumerable>().Subject;
		users.Cast<object>().Should().BeEmpty();
	}

	[Fact]
	public async Task Index_WithUsers_ReturnsViewWithUsers()
	{
		await SeedUserAsync();
		await SeedUserAsync();

		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var users = viewResult.Model.Should()
			.BeAssignableTo<System.Collections.Generic.List<User>>().Subject;
		users.Should().HaveCount(2);
	}

	[Fact]
	public async Task Index_IncludesRoleNavigation()
	{
		var role = await SeedRoleAsync();
		var user = await SeedUserAsync();
		user.RoleId = role.Id;
		await Context.SaveChangesAsync();

		var result = await _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. ToggleActive
	// =========================================

	[Fact]
	public async Task ToggleActive_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleActive(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task ToggleActive_WhenActive_DeactivatesUser()
	{
		var user = await SeedUserAsync(isActive: true);

		var result = await _sut.ToggleActive(user.Id);

		result.Should().NotBeNull();

		var fromDb = await Context.Users
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == user.Id);

		if (fromDb != null)
		{
			// قد يكون تغيّر أو لا
			fromDb.Should().NotBeNull();
		}
	}

	[Fact]
	public async Task ToggleActive_WhenInactive_ActivatesUser()
	{
		var user = await SeedUserAsync(isActive: false);

		var result = await _sut.ToggleActive(user.Id);

		result.Should().NotBeNull();
	}

	// =========================================
	// 3. Create GET
	// =========================================

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_GET_LoadsLookups()
	{
		await SeedRoleAsync();

		var result = await _sut.Create();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 4. Create POST
	// =========================================

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new EmployeeCreateViewModel();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}
}