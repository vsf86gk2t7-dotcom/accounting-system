using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper لاختبارات فاتورة الشراء — بيمنع تكرار الكود
/// </summary>
public static class PurchaseInvoiceTestHelpers
{
	/// <summary>
	/// البيانات الأساسية اللي بترجع من الزرع
	/// </summary>
	public record SeededPurchaseBaseData(
		int BranchId,
		int StoreId,
		int SupplierId,
		int ProductId,
		int UnitId);

	/// <summary>
	/// زرع البيانات الأساسية اللازمة لفاتورة شراء:
	/// ChartOfAccounts + FiscalPeriod + Unit + Branch + Store + Supplier + Product + ProductUnit
	/// </summary>
	public static async Task<SeededPurchaseBaseData> SeedPurchaseBaseDataAsync(
		CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		// =========================================
		// 0. Chart of Accounts
		// =========================================

		await DbSeeder.SeedChartOfAccountsAsync(db);

		// =========================================
		// 0.1. Fiscal Period
		// =========================================

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

		// =========================================
		// 1. Unit
		// =========================================

		var unit = new Unit
		{
			Name = "قطعة",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Units.Add(unit);
		await db.SaveChangesAsync();

		// =========================================
		// 2. Branch
		// =========================================

		var branch = new Branch
		{
			Name = "فرع اختبار مشتريات",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Branches.Add(branch);
		await db.SaveChangesAsync();

		// =========================================
		// 3. Store
		// =========================================

		var store = new Store
		{
			Name = "مخزن اختبار مشتريات",
			BranchId = branch.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Stores.Add(store);
		await db.SaveChangesAsync();

		// =========================================
		// 4. Supplier
		// =========================================

		var supplier = new Supplier
		{
			Name = "مورد اختبار",
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Suppliers.Add(supplier);
		await db.SaveChangesAsync();

		// =========================================
		// 5. Product + ProductUnit
		// =========================================

		var product = new Product
		{
			Name = "منتج اختبار مشتريات",
			Barcode = $"PURBAR{Random.Shared.Next(100000, 999999)}",
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

		return new SeededPurchaseBaseData(
			branch.Id,
			store.Id,
			supplier.Id,
			product.Id,
			unit.Id);
	}

	/// <summary>
	/// إنشاء فاتورة شراء Draft عبر HTTP — ترجّع الـ invoice ID
	/// </summary>
	public static async Task<int> CreateDraftPurchaseInvoiceViaHttpAsync(
		CustomWebApplicationFactory factory,
		SeededPurchaseBaseData baseData,
		HttpClient client,
		decimal quantity = 2,
		decimal unitPrice = 60)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/PurchaseInvoice/Create");

		var invoiceNumber = $"PUR-{Guid.NewGuid():N}".Substring(0, 15);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["InvoiceNumber"] = invoiceNumber,
			["SupplierId"] = baseData.SupplierId.ToString(),
			["BranchId"] = baseData.BranchId.ToString(),
			["StoreId"] = baseData.StoreId.ToString(),
			["InvoiceDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["DiscountAmount"] = "0",
			["TaxRate"] = "0",
			["TaxAmount"] = "0",
			["SalesTaxRate"] = "0",
			["SalesTaxAmount"] = "0",

			// ✅ Items binding
			["Items.Index"] = "0",
			["Items[0].ProductId"] = baseData.ProductId.ToString(),
			["Items[0].UnitId"] = baseData.UnitId.ToString(),
			["Items[0].Quantity"] = quantity.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["Items[0].UnitPrice"] = unitPrice.ToString(
				System.Globalization.CultureInfo.InvariantCulture),

			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/PurchaseInvoice/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateDraftPurchaseInvoiceViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var invoice = await db.PurchaseInvoices
			.FirstOrDefaultAsync(x => x.InvoiceNumber == invoiceNumber);

		if (invoice == null)
		{
			throw new InvalidOperationException(
				$"Purchase invoice {invoiceNumber} was not persisted.");
		}

		return invoice.Id;
	}
}