using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Admin;
using AccountingSystem.Services.Permissions;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace AccountingSystem.Tests.Controllers;

public class HomeControllerTests : BaseTest
{
	private readonly Mock<IWebHostEnvironment> _envMock;
	private readonly Mock<IPermissionService> _permissionMock;
	private readonly HomeController _sut;

	public HomeControllerTests()
	{
		_envMock = new Mock<IWebHostEnvironment>();
		_envMock.Setup(x => x.WebRootPath).Returns("C:\\temp");
		_envMock.Setup(x => x.ContentRootPath).Returns("C:\\temp");

		_permissionMock = new Mock<IPermissionService>();
		_permissionMock
			.Setup(x => x.HasPermissionAsync(It.IsAny<int>(), It.IsAny<string>()))
			.ReturnsAsync(false);

		_sut = new HomeController(Context, _envMock.Object, _permissionMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task<User> SeedAdminAsync(string password = "Admin@Test123!")
	{
		var hasher = new PasswordHasher<User>();

		var user = new User
		{
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			UserType = UserType.Admin,
			Status = UserStatus.Approved,
			IsActive = true,
			IsPasswordSet = true,
			SecurityStamp = Guid.NewGuid().ToString(),
			CreatedAt = DateTime.UtcNow
		};

		user.PasswordHash = hasher.HashPassword(user, password);

		Context.Users.Add(user);
		await Context.SaveChangesAsync();

		return user;
	}

	private void SetCurrentUser(int userId)
	{
		var claims = new[]
		{
			new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
			new Claim(ClaimTypes.Name, "TestAdmin"),
			new Claim(ClaimTypes.Role, "Admin")
		};
		var identity = new ClaimsIdentity(claims, "TestAuth");
		var principal = new ClaimsPrincipal(identity);

		var httpContext = new DefaultHttpContext { User = principal };
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
	}

	// =========================================
	// 1. Error
	// =========================================

	[Fact]
	public void Error_ReturnsViewResult()
	{
		var result = _sut.Error();
		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. Index
	// =========================================

	[Fact]
	public async Task Index_ReturnsResult()
	{
		var result = await _sut.Index(range: null);
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task Index_WithDifferentRanges_ReturnsResult()
	{
		foreach (var range in new[] { "today", "7d", "3m", "year", "month" })
		{
			var result = await _sut.Index(range);
			result.Should().NotBeNull();
		}
	}

	// =========================================
	// 3. FactoryResetData — GET
	// =========================================

	[Fact]
	public async Task FactoryResetData_ReturnsViewResult()
	{
		await SeedAdminAsync();
		SetCurrentUser(1);

		var result = await _sut.FactoryResetData();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task FactoryResetData_ReturnsModelWithCounts()
	{
		await SeedAdminAsync();
		SetCurrentUser(1);

		// ضيف بيانات
		Context.Branches.Add(new Branch
		{
			CompanyId = 1,
			Name = "فرع تجريبي",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		});
		await Context.SaveChangesAsync();

		var result = await _sut.FactoryResetData();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<AccountingSystem.Models.ViewModels.Admin.FactoryResetDataViewModel>()
			.Subject;

		model.Branches.Should().BeGreaterThanOrEqualTo(1);
	}

	// =========================================
	// 4. FactoryReset — POST (Validation paths)
	// =========================================

	[Fact]
	public async Task FactoryReset_WithInvalidModelState_Redirects()
	{
		SetCurrentUser(1);
		_sut.ModelState.AddModelError("test", "error");

		var model = new FactoryResetViewModel();

		var result = await _sut.FactoryReset(model);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task FactoryReset_WithNoCurrentUser_Redirects()
	{
		// مفيش claims
		var model = new FactoryResetViewModel
		{
			Password = "test",
			ConfirmationWord = HomeController.ResetConfirmationWord
		};

		var result = await _sut.FactoryReset(model);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task FactoryReset_WhenAdminNotFound_Redirects()
	{
		SetCurrentUser(99999);   // ⬅️ مش موجود

		var model = new FactoryResetViewModel
		{
			Password = "test",
			ConfirmationWord = HomeController.ResetConfirmationWord
		};

		var result = await _sut.FactoryReset(model);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task FactoryReset_WithWrongPassword_Redirects()
	{
		var admin = await SeedAdminAsync(password: "CorrectPassword123!");
		SetCurrentUser(admin.Id);

		var model = new FactoryResetViewModel
		{
			Password = "WrongPassword!",
			ConfirmationWord = HomeController.ResetConfirmationWord
		};

		var result = await _sut.FactoryReset(model);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task FactoryReset_WithWrongConfirmationWord_Redirects()
	{
		var admin = await SeedAdminAsync(password: "CorrectPassword123!");
		SetCurrentUser(admin.Id);

		var model = new FactoryResetViewModel
		{
			Password = "CorrectPassword123!",
			ConfirmationWord = "كلمة غلط"
		};

		var result = await _sut.FactoryReset(model);

		result.Should().BeOfType<RedirectToActionResult>();
	}
}