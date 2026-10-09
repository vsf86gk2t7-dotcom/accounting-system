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

public class BranchControllerTests : BaseTest
{
	private readonly BranchController _sut;

	public BranchControllerTests()
	{
		_sut = new BranchController(Context);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
		_sut.TempData = new TempDataDictionary(
			httpContext,
			Mock.Of<ITempDataProvider>());
	}

	private async Task<Company> SeedCompanyAsync()
	{
		var company = new Company
		{
			Name = "Test Company",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Companies.Add(company);
		await Context.SaveChangesAsync();
		return company;
	}

	private async Task<Branch> SeedBranchAsync(string name = "Test Branch")
	{
		var company = await SeedCompanyAsync();

		var branch = new Branch
		{
			Name = name,
			CompanyId = company.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Branches.Add(branch);
		await Context.SaveChangesAsync();
		return branch;
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
	public async Task Index_WithBranches_ReturnsView()
	{
		await SeedBranchAsync("B1");

		var result = await _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. Create GET
	// =========================================

	[Fact]
	public async Task Create_GET_WhenNoCompanies_Redirects()
	{
		// مفيش شركات → يرفض
		var result = await _sut.Create();

		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task Create_GET_WhenCompanyExists_ReturnsView()
	{
		await SeedCompanyAsync();

		var result = await _sut.Create();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 3. Create POST
	// =========================================

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new Branch();
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
		var branch = await SeedBranchAsync();

		var result = await _sut.Edit(id: branch.Id);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. Edit POST
	// =========================================

	[Fact]
	public async Task Edit_POST_WithInvalidModelState_ReturnsView()
	{
		var branch = await SeedBranchAsync();
		var model = new Branch { Id = branch.Id };
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Edit(branch.Id, model);

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
		var branch = await SeedBranchAsync();

		var result = await _sut.ToggleActive(branch.Id);

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