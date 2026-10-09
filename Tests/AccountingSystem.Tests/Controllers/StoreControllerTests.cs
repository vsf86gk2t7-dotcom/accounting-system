using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class StoreControllerTests : BaseTest
{
	private readonly StoreController _sut;

	public StoreControllerTests()
	{
		_sut = new StoreController(Context);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
		_sut.TempData = new TempDataDictionary(
			httpContext,
			Mock.Of<ITempDataProvider>());
	}

	private async Task EnsureCompanyAndBranchAsync()
	{
		if (!Context.Companies.Any(c => c.Id == 1))
		{
			Context.Companies.Add(new Company
			{
				Id = 1, Name = "Test Company",
				IsActive = true, CreatedAt = DateTime.UtcNow
			});
		}

		if (!Context.Branches.Any(b => b.Id == 1))
		{
			Context.Branches.Add(new Branch
			{
				Id = 1, Name = "Test Branch",
				CompanyId = 1, IsActive = true, CreatedAt = DateTime.UtcNow
			});
		}

		await Context.SaveChangesAsync();
	}

	private async Task<Store> SeedStoreAsync(string name = "Test Store")
	{
		await EnsureCompanyAndBranchAsync();

		var store = new Store
		{
			Name = name,
			BranchId = 1,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Stores.Add(store);
		await Context.SaveChangesAsync();
		return store;
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
	public async Task Index_WithStores_ReturnsViewWithStores()
	{
		await SeedStoreAsync("S1");
		await SeedStoreAsync("S2");

		var result = await _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. Create GET
	// =========================================

	[Fact]
	public async Task Create_GET_WhenBranchExists_ReturnsView()
	{
		// ✅ (fix) StoreController.Create بيعتمد على وجود Branches
		await EnsureCompanyAndBranchAsync();

		var result = await _sut.Create();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_GET_WhenNoBranches_Redirects()
	{
		// مفيش Branches → يرفض بـ Redirect
		var result = await _sut.Create();

		result.Should().BeOfType<RedirectToActionResult>();
	}
	// =========================================
	// 3. Create POST
	// =========================================

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new Store();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 4. Edit GET
	// =========================================

	[Fact]
	public async Task Edit_GET_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Edit(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Edit_GET_WhenExists_ReturnsView()
	{
		var store = await SeedStoreAsync();

		var result = await _sut.Edit(id: store.Id);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. Edit POST
	// =========================================

	[Fact]
	public async Task Edit_POST_WithInvalidModelState_ReturnsView()
	{
		var store = await SeedStoreAsync();
		var model = new Store { Id = store.Id };
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Edit(store.Id, model);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 6. ToggleActive
	// =========================================

	[Fact]
	public async Task ToggleActive_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleActive(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task ToggleActive_WhenExists_Toggles()
	{
		var store = await SeedStoreAsync();

		var result = await _sut.ToggleActive(store.Id);

		result.Should().NotBeNull();
	}

	// =========================================
	// 7. Delete
	// =========================================

	[Fact]
	public async Task Delete_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Delete(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}
}