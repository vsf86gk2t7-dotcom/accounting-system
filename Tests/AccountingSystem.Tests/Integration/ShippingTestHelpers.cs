using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper لاختبارات الشحن — بيعتمد على SalesInvoiceTestHelpers
/// </summary>
public static class ShippingTestHelpers
{
	public record SeededShippingBaseData(
		int ShippingCompanyId,
		int DriverId,
		int InvoiceId,
		int CashAccountId,
		int BranchId,
		int StoreId,
		int CustomerId,
		int ProductId,
		int UnitId);

	/// <summary>
	/// زرع البيانات الأساسية:
	/// Company + Branch + Store + Product + Unit + Employee(Driver) +
	/// ShippingCompany + فاتورة بيع مؤكدة
	/// </summary>
	public static async Task<SeededShippingBaseData> SeedShippingBaseDataAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		decimal invoiceQuantity = 2)
	{
		// 1. زرع بيانات الفاتورة (Branch, Store, Product, Unit, Customer, Cash)
		var invoiceBase = await SalesInvoiceTestHelpers
			.SeedInvoiceBaseDataAsync(factory);

		// 2. إنشاء فاتورة Draft + تأكيد
		var invoiceId = await SalesInvoiceTestHelpers
			.CreateDraftInvoiceViaHttpAsync(
				factory, invoiceBase, client);

		// 3. تأكيد الفاتورة
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/SalesInvoice/Details/{invoiceId}");

		var confirmForm = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		await client.PostAsync($"/SalesInvoice/Confirm/{invoiceId}", confirmForm);

		// 4. زرع Driver (Employee) + ShippingCompany
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var driver = new Employee
		{
			Name = $"سائق {Guid.NewGuid():N}".Substring(0, 15),
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Employees.Add(driver);
		await db.SaveChangesAsync();

		var shippingCompany = new ShippingCompany
		{
			Name = $"شركة شحن {Guid.NewGuid():N}".Substring(0, 15),
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			ShippingRate = 50,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.ShippingCompanies.Add(shippingCompany);
		await db.SaveChangesAsync();

		return new SeededShippingBaseData(
			shippingCompany.Id,
			driver.Id,
			invoiceId,
			invoiceBase.CashAccountId,
			invoiceBase.BranchId,
			invoiceBase.StoreId,
			invoiceBase.CustomerId,
			invoiceBase.ProductId,
			invoiceBase.UnitId);
	}

	/// <summary>
	/// إنشاء ShippingBill عبر HTTP — يرجّع الـ ShippingBill ID
	/// </summary>
	public static async Task<int> CreateShippingBillViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		SeededShippingBaseData baseData,
		DeliveryMethod deliveryMethod = DeliveryMethod.InternalDriver,
		string? notes = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Shipping/Create");

		var formData = new Dictionary<string, string>
		{
			["SalesInvoiceId"] = baseData.InvoiceId.ToString(),
			["DeliveryMethod"] = ((int)deliveryMethod).ToString(),
			["Notes"] = notes ?? "شحن اختبار",
			["DeliveryNotes"] = string.Empty,
			["ScheduledDate"] = string.Empty,

			["__RequestVerificationToken"] = token
		};

		if (deliveryMethod == DeliveryMethod.InternalDriver)
		{
			formData["DriverId"] = baseData.DriverId.ToString();
		}
		else
		{
			formData["ShippingCompanyId"] = baseData.ShippingCompanyId.ToString();
		}

		var form = new FormUrlEncodedContent(formData);

		var response = await client.PostAsync("/Shipping/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateShippingBillViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(600, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var bill = await db.ShippingBills
			.Where(x => x.SalesInvoiceId == baseData.InvoiceId)
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		if (bill == null)
		{
			throw new InvalidOperationException(
				$"ShippingBill for invoice {baseData.InvoiceId} was not persisted.");
		}

		return bill.Id;
	}
}