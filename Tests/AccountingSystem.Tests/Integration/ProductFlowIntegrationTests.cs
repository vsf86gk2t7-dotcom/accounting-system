using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// اختبارات تكامل للمنتجات
/// </summary>
public class ProductFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public ProductFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"product.view",
			"product.create",
			"product.edit",
			"product.activate")
			.GetAwaiter().GetResult();
	}

	// =========================================
	// 1. Index
	// =========================================

	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Product/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// =========================================
	// 2. Create via HTTP
	// =========================================

	[Fact]
	public async Task Create_Post_PersistsProductWithUnitsAndCategory()
	{
		var baseData = await ProductTestHelpers
			.SeedProductBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var barcode = $"BAR{Random.Shared.Next(100000, 999999)}";

		var productId = await ProductTestHelpers
			.CreateProductViaHttpAsync(
				_factory, client, baseData,
				name: "منتج جديد",
				barcode: barcode,
				minQuantity: 5,
				salePrice: 150);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var product = await db.Products
			.Include(x => x.ProductUnits)
			.Include(x => x.ProductCategories)
			.FirstOrDefaultAsync(x => x.Id == productId);

		product.Should().NotBeNull();
		product!.Name.Should().Be("منتج جديد");
		product.Barcode.Should().Be(barcode);
		product.BaseUnitId.Should().Be(baseData.UnitId);
		product.MinQuantity.Should().Be(5);
		product.IsActive.Should().BeTrue();

		// Unit
		product.ProductUnits.Should().HaveCount(1);
		var unit = product.ProductUnits.First();
		unit.UnitId.Should().Be(baseData.UnitId);
		unit.SalePrice.Should().Be(150);
		unit.ConversionFactor.Should().Be(1);

		// Category
		product.ProductCategories.Should().HaveCount(1);
		product.ProductCategories.First().CategoryId.Should().Be(baseData.CategoryId);
	}

	// =========================================
	// 3. Edit
	// =========================================

	[Fact]
	public async Task Edit_Post_UpdatesProduct()
	{
		var baseData = await ProductTestHelpers
			.SeedProductBaseDataAsync(_factory);

		var productId = await ProductTestHelpers
			.SeedProductAsync(
				_factory,
				name: "قبل التعديل",
				unitId: baseData.UnitId,
				categoryId: baseData.CategoryId,
				salePrice: 100);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Product/Edit/{productId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Id"] = productId.ToString(),
			["Name"] = "بعد التعديل",
			["Barcode"] = "NEW-BAR-001",
			["Code"] = "PRD-001",
			["Description"] = "وصف جديد",
			["MinQuantity"] = "10",
			["BaseUnitId"] = baseData.UnitId.ToString(),

			// Units
			["Units.Index"] = "0",
			["Units[0].UnitId"] = baseData.UnitId.ToString(),
			["Units[0].ConversionFactor"] = "1",
			["Units[0].SalePrice"] = "250",
			["Units[0].IsActive"] = "true",

			// Category
			["CategoryIds[0]"] = baseData.CategoryId.ToString(),

			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Product/Edit", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther },
			$"Status={response.StatusCode}, Location={response.Headers.Location}");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var updated = await db.Products
			.Include(x => x.ProductUnits)
			.FirstOrDefaultAsync(x => x.Id == productId);

		updated.Should().NotBeNull();
		updated!.Name.Should().Be("بعد التعديل");
		updated.Barcode.Should().Be("NEW-BAR-001");
		updated.Code.Should().Be("PRD-001");
		updated.Description.Should().Be("وصف جديد");
		updated.MinQuantity.Should().Be(10);

		// SalePrice اتحدّث
		updated.ProductUnits.First().SalePrice.Should().Be(250);
	}

	// =========================================
	// 4. ToggleActive
	// =========================================

	[Fact]
	public async Task ToggleActive_ExistingProduct_ChangesState()
	{
		var baseData = await ProductTestHelpers
			.SeedProductBaseDataAsync(_factory);

		var productId = await ProductTestHelpers
			.SeedProductAsync(
				_factory,
				unitId: baseData.UnitId);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Product/Edit/{productId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Product/ToggleActive/{productId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var product = await db.Products.FindAsync(productId);

		product!.IsActive.Should().BeFalse();
	}

	// =========================================
	// 5. Validation: Name empty
	// =========================================

	[Fact]
	public async Task Create_WithEmptyName_ReturnsViewWithError()
	{
		var baseData = await ProductTestHelpers
			.SeedProductBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Product/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = "",
			["BaseUnitId"] = baseData.UnitId.ToString(),
			["Units.Index"] = "0",
			["Units[0].UnitId"] = baseData.UnitId.ToString(),
			["Units[0].ConversionFactor"] = "1",
			["Units[0].SalePrice"] = "100",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Product/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// ✅ تحقق إن المنتج اللي بالاسم الفاضي ماتحفظش
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var anyWithEmptyName = await db.Products
			.AnyAsync(x => x.Name == string.Empty);

		anyWithEmptyName.Should().BeFalse(
			"empty-name product should not be persisted");
	}

	// =========================================
	// 6. Validation: Barcode duplicate
	// =========================================

	[Fact]
	public async Task Create_WithDuplicateBarcode_ReturnsViewWithError()
	{
		var baseData = await ProductTestHelpers
			.SeedProductBaseDataAsync(_factory);

		// منتج موجود بنفس الباركود
		var existingBarcode = "DUPLICATE-BAR";
		var existing = await ProductTestHelpers
			.SeedProductAsync(_factory, unitId: baseData.UnitId);

		// اعمل تحديث للـ Barcode
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var prod = await db.Products.FindAsync(existing);
			prod!.Barcode = existingBarcode;
			await db.SaveChangesAsync();
		}

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Product/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = "منتج مكرر",
			["Barcode"] = existingBarcode,   // ⬅️ مكرر
			["BaseUnitId"] = baseData.UnitId.ToString(),
			["Units.Index"] = "0",
			["Units[0].UnitId"] = baseData.UnitId.ToString(),
			["Units[0].ConversionFactor"] = "1",
			["Units[0].SalePrice"] = "100",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Product/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// ✅ تحقق إن المنتج المكرر ما اتحفظش (بس الأصلي موجود)
		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var productsWithBarcode = await db2.Products
			.Where(x => x.Barcode == existingBarcode)
			.ToListAsync();

		productsWithBarcode.Should().HaveCount(1,
			"only the original product should have this barcode");
	}
}