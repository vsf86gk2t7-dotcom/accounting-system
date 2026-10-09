using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper لاختبارات مرتجع البيع — بيبني على SalesInvoiceTestHelpers
/// </summary>
public static class SalesReturnTestHelpers
{
	public record SeededSalesReturnBaseData(
		int InvoiceId,
		int SalesInvoiceItemId,
		int CustomerId,
		int StoreId,
		int ProductId,
		int UnitId,
		int CashAccountId,
		decimal SoldQuantity);

	/// <summary>
	/// زرع البيانات الأساسية لمرتجع بيع:
	/// ينشئ فاتورة بيع مؤكدة (Draft → Confirm) + يجيب SalesInvoiceItemId
	/// </summary>
	public static async Task<SeededSalesReturnBaseData> SeedConfirmedInvoiceAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		decimal quantity = 10)
	{
		// 1. استخدام SalesInvoiceTestHelpers لزرع البيانات الأساسية
		var invoiceBaseData = await SalesInvoiceTestHelpers
			.SeedInvoiceBaseDataAsync(factory);

		// 2. إنشاء فاتورة Draft
				// 2. إنشاء فاتورة Draft
		// ملاحظة: SalesInvoiceTestHelpers.CreateDraftInvoiceViaHttpAsync
		// بتستخدم quantity ثابتة (2) و unitPrice ثابتة (100)
		var invoiceId = await SalesInvoiceTestHelpers
			.CreateDraftInvoiceViaHttpAsync(
				factory, invoiceBaseData, client);

		// 3. تأكيد الفاتورة
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/SalesInvoice/Details/{invoiceId}");

		var confirmForm = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var confirmResponse = await client.PostAsync(
			$"/SalesInvoice/Confirm/{invoiceId}", confirmForm);

		if (confirmResponse.StatusCode != System.Net.HttpStatusCode.Redirect &&
			confirmResponse.StatusCode != System.Net.HttpStatusCode.Found &&
			confirmResponse.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			throw new InvalidOperationException(
				$"Confirm invoice failed. Status={confirmResponse.StatusCode}");
		}

		// 4. جلب SalesInvoiceItemId من الفاتورة المؤكدة
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var invoice = await db.SalesInvoices
			.Include(x => x.Items)
			.FirstOrDefaultAsync(x => x.Id == invoiceId);

		if (invoice == null)
		{
			throw new InvalidOperationException(
				$"Invoice {invoiceId} not found after confirm.");
		}

		if (invoice.Status != SalesInvoiceStatus.Confirmed)
		{
			throw new InvalidOperationException(
				$"Invoice {invoiceId} status is {invoice.Status}, expected Confirmed.");
		}

		var firstItem = invoice.Items.FirstOrDefault();

		if (firstItem == null)
		{
			throw new InvalidOperationException(
				$"Invoice {invoiceId} has no items.");
		}

		return new SeededSalesReturnBaseData(
			invoiceId,
			firstItem.Id,
			invoice.CustomerId ?? 0,
			invoice.StoreId,
			firstItem.ProductId,
			firstItem.UnitId,
			invoiceBaseData.CashAccountId,
			firstItem.Quantity);
	}

	/// <summary>
	/// إنشاء مرتجع بيع عبر HTTP — يرجّع SalesReturnInvoice ID
	/// </summary>
	public static async Task<int> CreateSalesReturnViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		SeededSalesReturnBaseData baseData,
		decimal quantity = 2,
		bool refundToCash = false,
		string? reason = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, "/SalesReturn/Create");

		var formData = new Dictionary<string, string>
		{
			["OriginalSalesInvoiceId"] = baseData.InvoiceId.ToString(),
			["CustomerId"] = baseData.CustomerId.ToString(),
			["StoreId"] = baseData.StoreId.ToString(),
			["ReturnDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Reason"] = reason ?? "مرتجع بيع اختبار",
			["RefundToCash"] = refundToCash ? "true" : "false",
			["RefundCashAccountId"] = refundToCash
				? baseData.CashAccountId.ToString()
				: string.Empty,
			["RefundAmount"] = "0",

			["Items.Index"] = "0",
			["Items[0].SalesInvoiceItemId"] = baseData.SalesInvoiceItemId.ToString(),
			["Items[0].ProductId"] = baseData.ProductId.ToString(),
			["Items[0].UnitId"] = baseData.UnitId.ToString(),
			["Items[0].ConversionFactor"] = "1",
			["Items[0].Quantity"] = quantity.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["Items[0].UnitPrice"] = "100",

			["__RequestVerificationToken"] = token
		};

		var form = new FormUrlEncodedContent(formData);

		var response = await client.PostAsync("/SalesReturn/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();

			// احفظ الـ body في ملف للتشخيص
			var debugPath = Path.Combine(
				Path.GetTempPath(),
				$"sales-return-response-{Guid.NewGuid():N}.html");
			File.WriteAllText(debugPath, body);

			// استخرج رسائل الـ validation
			var errors = System.Text.RegularExpressions.Regex
				.Matches(body,
					@"<span[^>]*class=""[^""]*field-validation-error[^""]*""[^>]*>([^<]+)</span>",
					System.Text.RegularExpressions.RegexOptions.IgnoreCase)
				.Cast<System.Text.RegularExpressions.Match>()
				.Select(m => m.Groups[1].Value.Trim())
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.ToList();

			var summary = System.Text.RegularExpressions.Regex
				.Matches(body,
					@"<div[^>]*class=""[^""]*validation-summary-errors[^""]*""[^>]*>([\s\S]*?)</div>",
					System.Text.RegularExpressions.RegexOptions.IgnoreCase)
				.Cast<System.Text.RegularExpressions.Match>()
				.Select(m => System.Text.RegularExpressions.Regex.Replace(
					m.Groups[1].Value, "<[^>]+>", " ").Trim())
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.ToList();

			throw new InvalidOperationException(
				$"CreateSalesReturnViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"FieldErrors=[{string.Join(" | ", errors)}]. " +
				$"SummaryErrors=[{string.Join(" | ", summary)}]. " +
				$"BodyFile={debugPath}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var returnInvoice = await db.SalesReturnInvoices
			.Where(x => x.OriginalSalesInvoiceId == baseData.InvoiceId)
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		if (returnInvoice == null)
		{
			throw new InvalidOperationException(
				"SalesReturnInvoice was not persisted.");
		}

		return returnInvoice.Id;
	}
}