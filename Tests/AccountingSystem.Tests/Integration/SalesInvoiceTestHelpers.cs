using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

public static class SalesInvoiceTestHelpers
{
	public record SeededInvoiceBaseData(
		int BranchId,
		int StoreId,
		int CustomerId,
		int ProductId,
		int UnitId,
		int StockLotId,
		int CashAccountId);

	public static async Task<SeededInvoiceBaseData> SeedInvoiceBaseDataAsync(
		CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		// =========================================
		// 0. زرع شجرة الحسابات (مطلوبة للـ PostingService)
		// =========================================

		await DbSeeder.SeedChartOfAccountsAsync(db);
// زرع فترة محاسبية مفتوحة (مطلوبة للـ PostingService)
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

		// 2. Branch
		var branch = new Branch
		{
			Name = "فرع اختبار",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Branches.Add(branch);
		await db.SaveChangesAsync();

		// 3. Store
		var store = new Store
		{
			Name = "مخزن اختبار",
			BranchId = branch.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Stores.Add(store);
		await db.SaveChangesAsync();

		// 4. ChartAccount → CashAccount
		var chartAccount = new ChartAccount
		{
			Name = "صندوق اختبار",
			Code = $"CASH_{Guid.NewGuid():N}".Substring(0, 20),
			Type = AccountType.Asset,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.ChartAccounts.Add(chartAccount);
		await db.SaveChangesAsync();

		var cashAccount = new CashAccount
		{
			Name = "صندوق اختبار",
			ChartAccountId = chartAccount.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.CashAccounts.Add(cashAccount);
		await db.SaveChangesAsync();

		// 5. Customer
		var customer = new Customer
		{
			Name = "عميل اختبار",
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Customers.Add(customer);
		await db.SaveChangesAsync();

		// 6. Product + ProductUnit
		var product = new Product
		{
			Name = "منتج اختبار",
			Barcode = $"BAR{Random.Shared.Next(100000, 999999)}",
			BaseUnitId = unit.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Products.Add(product);
		await db.SaveChangesAsync();

		var productUnit = new ProductUnit
		{
			ProductId = product.Id,
			UnitId = unit.Id,
			ConversionFactor = 1,
			SalePrice = 100,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.ProductUnits.Add(productUnit);
		await db.SaveChangesAsync();

		// 7. StockLot
		var stockLot = new StockLot
		{
			StoreId = store.Id,
			ProductId = product.Id,
			QuantityReceived = 100,
			QuantityRemaining = 100,
			UnitCost = 60,
			PurchaseDate = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
			IsActive = true
		};
		db.StockLots.Add(stockLot);
		await db.SaveChangesAsync();

		return new SeededInvoiceBaseData(
			branch.Id,
			store.Id,
			customer.Id,
			product.Id,
			unit.Id,
			stockLot.Id,
			cashAccount.Id);
	}

	/// <summary>
	/// إنشاء فاتورة بيع Draft عبر HTTP — ترجّع الـ invoice ID
	/// </summary>
	public static async Task<int> CreateDraftInvoiceViaHttpAsync(
		CustomWebApplicationFactory factory,
		SeededInvoiceBaseData baseData,
		HttpClient client,
		SalesPaymentMethod paymentMethod = SalesPaymentMethod.Credit)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/SalesInvoice/Create");

		var invoiceNumber = $"INV-{Guid.NewGuid():N}".Substring(0, 15);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["InvoiceNumber"] = invoiceNumber,
			["BranchId"] = baseData.BranchId.ToString(),
			["StoreId"] = baseData.StoreId.ToString(),
			["CustomerId"] = baseData.CustomerId.ToString(),
			["InvoiceDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["PaymentMethod"] = ((int)paymentMethod).ToString(),
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["DiscountAmount"] = "0",
			["TaxAmount"] = "0",
			["SalesTaxRate"] = "0",
			["TaxRate"] = "0",

			// ✅ Items binding
			["Items.Index"] = "0",
			["Items[0].ProductId"] = baseData.ProductId.ToString(),
			["Items[0].UnitId"] = baseData.UnitId.ToString(),
			["Items[0].Quantity"] = "2",
			["Items[0].UnitPrice"] = "100",
			["Items[0].IssueMethod"] = "1", // FIFO

			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/SalesInvoice/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateDraftInvoiceViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var invoice = await db.SalesInvoices
			.FirstOrDefaultAsync(x => x.InvoiceNumber == invoiceNumber);

		if (invoice == null)
		{
			throw new InvalidOperationException(
				$"Invoice {invoiceNumber} was not persisted.");
		}

		return invoice.Id;
	}
}