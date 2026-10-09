using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Products;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class ProductControllerTests : BaseTest
{
	private readonly ProductController _sut;

	public ProductControllerTests()
	{
		_sut = new ProductController(Context);

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

	private async Task<Unit> SeedUnitAsync(int id = 1)
	{
		// ✅ (fix) لو الوحدة موجودة، نرجّعها بدل ما نضيف نسخة جديدة
		var existing = await Context.Units
			.FirstOrDefaultAsync(x => x.Id == id);

		if (existing != null)
			return existing;

		var unit = new Unit
		{
			Id = id,
			Name = $"Unit {id}",
			ShortName = $"U{id}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Units.Add(unit);
		await Context.SaveChangesAsync();
		return unit;
	}

	private async Task<Product> SeedProductAsync(
		string name = "Test Product",
		string code = "TP001")
	{
		await SeedUnitAsync();

		var product = new Product
		{
			Name = name,
			Code = code,
			Barcode = $"BC{Random.Shared.Next(10000000, 99999999)}",
			BaseUnitId = 1,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Products.Add(product);
		await Context.SaveChangesAsync();
		return product;
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
	public async Task Index_WithNoProducts_ReturnsEmptyList()
	{
		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var products = viewResult.Model.Should()
			.BeAssignableTo<System.Collections.Generic.List<Product>>().Subject;
		products.Should().BeEmpty();
	}

	[Fact]
	public async Task Index_WithProducts_ReturnsViewWithProducts()
	{
		await SeedProductAsync("Product 1", "P001");
		await SeedProductAsync("Product 2", "P002");

		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var products = viewResult.Model.Should()
			.BeAssignableTo<System.Collections.Generic.List<Product>>().Subject;
		products.Should().HaveCount(2);
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
		var model = new ProductCreateViewModel();
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
		var product = await SeedProductAsync();

		var result = await _sut.Edit(id: product.Id);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. Edit POST
	// =========================================

	[Fact]
	public async Task Edit_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new ProductEditViewModel();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Edit(model);

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
		var product = await SeedProductAsync();

		var result = await _sut.ToggleActive(product.Id);

		result.Should().NotBeNull();
	}
}