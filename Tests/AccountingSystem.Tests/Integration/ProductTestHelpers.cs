using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper لاختبارات المنتجات — بيمنع تكرار الكود
/// </summary>
public static class ProductTestHelpers
{
	/// <summary>
	/// البيانات الأساسية اللي بترجع من الزرع
	/// </summary>
	public record SeededProductBaseData(
		int UnitId,
		int CategoryId);

	/// <summary>
	/// زرع البيانات الأساسية (Unit + Category)
	/// </summary>
	public static async Task<SeededProductBaseData> SeedProductBaseDataAsync(
		CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		// 1. Unit
		var unit = new Unit
		{
			Name = "قطعة",
			ShortName = "قطعة",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Units.Add(unit);
		await db.SaveChangesAsync();

		// 2. Category
		var category = new Category
		{
			Name = "تصنيف اختبار",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Categories.Add(category);
		await db.SaveChangesAsync();

		return new SeededProductBaseData(unit.Id, category.Id);
	}

	/// <summary>
	/// زرع منتج مباشرة في قاعدة البيانات (بدون HTTP) — ترجّع الـ ID
	/// </summary>
	public static async Task<int> SeedProductAsync(
		CustomWebApplicationFactory factory,
		string name = "منتج اختبار",
		int? unitId = null,
		int? categoryId = null,
		decimal salePrice = 100)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		// تأكد إن Unit موجود
		if (unitId == null)
		{
			var unit = new Unit
			{
				Name = $"قطعة-{Guid.NewGuid():N}".Substring(0, 15),
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};
			db.Units.Add(unit);
			await db.SaveChangesAsync();
			unitId = unit.Id;
		}

		var product = new Product
		{
			Name = name,
			Barcode = $"BAR{Random.Shared.Next(100000, 999999)}",
			BaseUnitId = unitId.Value,
			MinQuantity = 0,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Products.Add(product);
		await db.SaveChangesAsync();

		// ProductUnit
		db.ProductUnits.Add(new ProductUnit
		{
			ProductId = product.Id,
			UnitId = unitId.Value,
			ConversionFactor = 1,
			SalePrice = salePrice,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		});
		await db.SaveChangesAsync();

		// ProductCategory (لو categoryId متمرر)
		if (categoryId.HasValue)
		{
			db.ProductCategories.Add(new ProductCategory
			{
				ProductId = product.Id,
				CategoryId = categoryId.Value,
				CreatedAt = DateTime.UtcNow
			});
			await db.SaveChangesAsync();
		}

		return product.Id;
	}

	/// <summary>
	/// إنشاء منتج عبر HTTP — ترجّع الـ product ID
	/// </summary>
	public static async Task<int> CreateProductViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		SeededProductBaseData baseData,
		string name = "منتج HTTP",
		string? barcode = null,
		decimal minQuantity = 0,
		decimal salePrice = 100,
		bool includeCategory = true)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Product/Create");

		barcode ??= $"BAR{Random.Shared.Next(100000, 999999)}";

		var formData = new Dictionary<string, string>
		{
			["Name"] = name,
			["Barcode"] = barcode,
			["Code"] = string.Empty,
			["Description"] = string.Empty,
			["MinQuantity"] = minQuantity.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["BaseUnitId"] = baseData.UnitId.ToString(),

			// Units[0]
			["Units.Index"] = "0",
			["Units[0].UnitId"] = baseData.UnitId.ToString(),
			["Units[0].ConversionFactor"] = "1",
			["Units[0].SalePrice"] = salePrice.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["Units[0].IsActive"] = "true",

			["__RequestVerificationToken"] = token
		};

		if (includeCategory)
		{
			formData["CategoryIds[0]"] = baseData.CategoryId.ToString();
		}

		var form = new FormUrlEncodedContent(formData);

		var response = await client.PostAsync("/Product/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateProductViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var product = await db.Products
			.FirstOrDefaultAsync(x => x.Barcode == barcode);

		if (product == null)
		{
			throw new InvalidOperationException(
				$"Product with barcode {barcode} was not persisted.");
		}

		return product.Id;
	}
}