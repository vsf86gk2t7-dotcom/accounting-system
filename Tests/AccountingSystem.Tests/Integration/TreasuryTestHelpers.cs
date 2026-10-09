using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper لاختبارات الخزينة — بيمنع تكرار الكود
/// </summary>
public static class TreasuryTestHelpers
{
	public record SeededTreasuryBaseData(
		int CashAccountId,
		int CustomerId,
		int ExpenseAccountId);

	/// <summary>
	/// زرع البيانات الأساسية:
	/// ChartOfAccounts + FiscalPeriod + CashAccount (مرتبط بـ 1101) + Customer + ExpenseAccount
	/// </summary>
	public static async Task<SeededTreasuryBaseData> SeedTreasuryBaseDataAsync(
		CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		// 0. Chart of Accounts
		await DbSeeder.SeedChartOfAccountsAsync(db);

		// 0.1. Fiscal Period
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

		// 1. Cash Chart Account (1101 — الصندوق)
		var cashChartAccount = await db.ChartAccounts
			.FirstOrDefaultAsync(x => x.Code == "1101" && x.IsActive);

		if (cashChartAccount == null)
		{
			throw new InvalidOperationException(
				"Chart account 1101 (Cash) not found. Check DbSeeder.");
		}

		// 2. CashAccount مرتبط بـ ChartAccount
		var cashAccount = new CashAccount
		{
			Name = $"صندوق اختبار {Guid.NewGuid():N}".Substring(0, 20),
			Provider = WalletProvider.General,
			ChartAccountId = cashChartAccount.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.CashAccounts.Add(cashAccount);
		await db.SaveChangesAsync();

		// 3. Customer
		var customer = new Customer
		{
			Name = $"عميل اختبار {Guid.NewGuid():N}".Substring(0, 20),
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Customers.Add(customer);
		await db.SaveChangesAsync();

		// 4. Expense Account
		var expenseAccount = await db.ChartAccounts
			.AsNoTracking()
			.FirstOrDefaultAsync(x =>
				x.Type == AccountType.Expense &&
				x.IsActive);

		return new SeededTreasuryBaseData(
			cashAccount.Id,
			customer.Id,
			expenseAccount?.Id ?? 0);
	}

	/// <summary>
	/// تسجيل إيصال قبض عبر HTTP — يرجّع الـ TreasuryTransaction ID
	/// </summary>
	public static async Task<int> ReceiveViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		SeededTreasuryBaseData baseData,
		decimal amount = 100,
		string? reason = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Receive");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = amount.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["CustomerId"] = baseData.CustomerId.ToString(),
			["SupplierId"] = string.Empty,
			["EmployeeId"] = string.Empty,
			["OtherIncomeAccountId"] = string.Empty,
			["Reason"] = reason ?? "قبض اختبار",
			["ReferenceDocument"] = string.Empty,
			["ApplyCommission"] = "false",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Receive", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"ReceiveViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(600, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var transaction = await db.TreasuryTransactions
			.Where(x => x.Type == TreasuryTransactionType.Receive)
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		if (transaction == null)
		{
			throw new InvalidOperationException(
				"Treasury Receive was not persisted.");
		}

		return transaction.Id;
	}

	/// <summary>
	/// تسجيل إذن صرف عبر HTTP — يرجّع الـ TreasuryTransaction ID
	/// ملاحظة: لازم يكون في رصيد كافي (اعمل Receive الأول)
	/// </summary>
	public static async Task<int> PayViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		SeededTreasuryBaseData baseData,
		decimal amount = 50,
		string? reason = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Pay");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = amount.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["CustomerId"] = baseData.CustomerId.ToString(),   // ✅ استخدم Customer
			["SupplierId"] = string.Empty,
			["EmployeeId"] = string.Empty,
			["ExpenseAccountId"] = string.Empty,               // ✅ فرّغها
			["Reason"] = reason ?? "صرف اختبار",
			["ReferenceDocument"] = string.Empty,
			["WalletFee"] = string.Empty,
			["RecipientWalletProvider"] = string.Empty,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Pay", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"PayViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(600, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var transaction = await db.TreasuryTransactions
			.Where(x => x.Type == TreasuryTransactionType.Pay)
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		if (transaction == null)
		{
			throw new InvalidOperationException(
				"Treasury Pay was not persisted.");
		}

		return transaction.Id;
	}

	/// <summary>
	/// تسوية خزينة عبر HTTP — يرجّع الـ TreasuryTransaction ID
	/// </summary>
	public static async Task<int> AdjustViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		SeededTreasuryBaseData baseData,
		decimal amount = 100,
		bool isIncrease = true,
		string? reason = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Adjust");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = amount.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["IsIncrease"] = isIncrease ? "true" : "false",
			["Reason"] = reason ?? "تسوية اختبار",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Adjust", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"AdjustViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(600, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var transaction = await db.TreasuryTransactions
			.Where(x => x.Type == TreasuryTransactionType.Adjust)
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		if (transaction == null)
		{
			throw new InvalidOperationException(
				"Treasury Adjust was not persisted.");
		}

		return transaction.Id;
	}
}