using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class CompanyControllerTests : BaseTest
{
	private readonly Mock<IWebHostEnvironment> _envMock;
	private readonly CompanyController _sut;

	public CompanyControllerTests()
	{
		_envMock = new Mock<IWebHostEnvironment>();
		_envMock.Setup(x => x.WebRootPath).Returns("C:\\temp");

		_sut = new CompanyController(
			Context,
			_envMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
		_sut.TempData = new TempDataDictionary(
			httpContext,
			Mock.Of<ITempDataProvider>());
	}

	private async Task<Company> SeedCompanyAsync(string name = "Test Company")
	{
		var company = new Company
		{
			Name = name,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Companies.Add(company);
		await Context.SaveChangesAsync();
		return company;
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
	public async Task Index_WithCompanies_ReturnsView()
	{
		await SeedCompanyAsync("C1");
		await SeedCompanyAsync("C2");

		var result = await _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. Create GET
	// =========================================

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 3. Create POST
	// =========================================

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new Company();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model, logo: null);

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
		var company = await SeedCompanyAsync();

		var result = await _sut.Edit(id: company.Id);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. Edit POST
	// =========================================

	[Fact]
	public async Task Edit_POST_WithInvalidModelState_ReturnsView()
	{
		var company = await SeedCompanyAsync();
		var model = new Company { Id = company.Id };
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Edit(company.Id, model, logo: null);

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
		var company = await SeedCompanyAsync();

		var result = await _sut.ToggleActive(company.Id);

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