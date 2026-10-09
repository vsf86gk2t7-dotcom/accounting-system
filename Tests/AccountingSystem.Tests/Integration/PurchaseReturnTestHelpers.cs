using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

public static class PurchaseReturnTestHelpers
{
	public record SeededPurchaseReturnBaseData(
		int StoreId,
		int SupplierId,
		int ProductId,
		int StockLotId,
		int UnitId);

	/// <summary>
	/// زرع البيانات الأساسية لمرتجع شراء:
	/// ChartOfAccounts + FiscalPeriod + Branch + Store + Supplier + Product + Unit + StockLot
	/// </summary>
	public static async Task<SeededPurchaseReturnBaseData> SeedPurchaseReturnBaseDataAsync(
		CustomWebApplicationFactory factory,
		decimal stockQuantity = 100,
		decimal unitCost = 60)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		// 0. Chart of Accounts + FiscalPeriod
		await DbSeeder.SeedChartOfAccountsAsync(db);

		var today = DateTime.Today;
		var hasOpenPeriod = await db.FiscalPeriods
			.AnyAsync(x =>
				x.StartDate <= today &&
				x.EndDate >= today &&
				!x.IsClosed);

		if (!hasOpenPeriod)
		{
			db.FiscalPeriods.Add(new FiscalPeriod
			{
				PeriodName = today.ToString("yyyy-MM"),
				StartDate = new DateTime(today.Year, today.Month, 1),
				EndDate = new DateTime(today.Year, today.Month, 1)
					.AddMonths(1).AddDays(-1),
				IsClosed = false,
				CreatedAt = DateTime.UtcNow
			});
			await db.SaveChangesAsync();
		}

		// 1. Unit
		var unit = new Unit
		{
			Name = "قطعة",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Units.Add(unit);
		await db.SaveChangesAsync();

		// 2. Company + Branch
		var company = await db.Companies.FirstOrDefaultAsync();
		if (company == null)
		{
			company = new Company
			{
				Name = "شركة اختبار مرتجعات",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};
			db.Companies.Add(company);
			await db.SaveChangesAsync();
		}

		var branch = new Branch
		{
			CompanyId = company.Id,
			Name = $"فرع مرتجعات {Guid.NewGuid():N}".Substring(0, 20),
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Branches.Add(branch);
		await db.SaveChangesAsync();

		// 3. Store
		var store = new Store
		{
			Name = $"مخزن مرتجعات {Guid.NewGuid():N}".Substring(0, 20),
			BranchId = branch.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Stores.Add(store);
		await db.SaveChangesAsync();

		// 4. Supplier
		var supplier = new Supplier
		{
			Name = $"مورد مرتجعات {Guid.NewGuid():N}".Substring(0, 20),
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Suppliers.Add(supplier);
		await db.SaveChangesAsync();

		// 5. Product + ProductUnit
		var product = new Product
		{
			Name = $"منتج مرتجعات {Guid.NewGuid():N}".Substring(0, 20),
			Barcode = $"RETBAR{Random.Shared.Next(100000, 999999)}",
			BaseUnitId = unit.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Products.Add(product);
		await db.SaveChangesAsync();

		db.ProductUnits.Add(new ProductUnit
		{
			ProductId = product.Id,
			UnitId = unit.Id,
			ConversionFactor = 1,
			SalePrice = 100,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		});
		await db.SaveChangesAsync();

		// 6. StockLot — لازم يكون فيه كمية للإرجاع
		var stockLot = new StockLot
		{
			StoreId = store.Id,
			ProductId = product.Id,
			SupplierId = supplier.Id,
			QuantityReceived = stockQuantity,
			QuantityRemaining = stockQuantity,
			UnitCost = unitCost,
			PurchaseDate = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
			IsActive = true
		};
		db.StockLots.Add(stockLot);
		await db.SaveChangesAsync();

		return new SeededPurchaseReturnBaseData(
			store.Id,
			supplier.Id,
			product.Id,
			stockLot.Id,
			unit.Id);
	}

	/// <summary>
	/// إنشاء مرتجع شراء عبر HTTP — يرجّع الـ PurchaseReturnInvoice ID
	/// </summary>
	public static async Task<int> CreatePurchaseReturnViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		SeededPurchaseReturnBaseData baseData,
		decimal quantity = 10,
		string? reason = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/PurchaseReturn/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["SupplierId"] = baseData.SupplierId.ToString(),
			["StoreId"] = baseData.StoreId.ToString(),
			["ReturnDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Reason"] = reason ?? "مرتجع اختبار",

			["Items.Index"] = "0",
			["Items[0].ProductId"] = baseData.ProductId.ToString(),
			["Items[0].Quantity"] = quantity.ToString(
				System.Globalization.CultureInfo.InvariantCulture),

			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/PurchaseReturn/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreatePurchaseReturnViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(600, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var invoice = await db.PurchaseReturnInvoices
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		if (invoice == null)
		{
			throw new InvalidOperationException(
				"PurchaseReturnInvoice was not persisted.");
		}

		return invoice.Id;
	}
}