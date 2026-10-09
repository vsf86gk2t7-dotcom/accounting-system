using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

public static class EmployeeAdvanceTestHelpers
{
	public record SeededAdvanceBaseData(
		int EmployeeId,
		int CashAccountId);

	/// <summary>
	/// زرع البيانات الأساسية للسلفة:
	/// ChartOfAccounts + FiscalPeriod + Employee + CashAccount مرتبط بـ 1101
	/// </summary>
	public static async Task<SeededAdvanceBaseData> SeedAdvanceBaseDataAsync(
		CustomWebApplicationFactory factory,
		decimal salary = 5000)
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

		// 1. Cash Chart Account (1101)
		var cashChartAccount = await db.ChartAccounts
			.FirstOrDefaultAsync(x => x.Code == "1101" && x.IsActive);

		if (cashChartAccount == null)
			throw new InvalidOperationException("Chart account 1101 not found.");

		// 2. CashAccount
		var cashAccount = new CashAccount
		{
			Name = $"خزنة سلف {Guid.NewGuid():N}".Substring(0, 20),
			Provider = WalletProvider.General,
			ChartAccountId = cashChartAccount.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.CashAccounts.Add(cashAccount);
		await db.SaveChangesAsync();

		// 3. Employee
		var employee = new Employee
		{
			Name = $"موظف سلف {Guid.NewGuid():N}".Substring(0, 20),
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			Salary = salary,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Employees.Add(employee);
		await db.SaveChangesAsync();

		return new SeededAdvanceBaseData(employee.Id, cashAccount.Id);
	}

	/// <summary>
	/// صرف سلفة عبر HTTP
	/// </summary>
	public static async Task<HttpResponseMessage> CreateAdvanceViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		SeededAdvanceBaseData baseData,
		decimal amount = 500,
		string? reason = "سلفة اختبار")
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/EmployeeAdvance/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["employeeId"] = baseData.EmployeeId.ToString(),
			["amount"] = amount.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["date"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["reason"] = reason ?? string.Empty,
			["cashAccountId"] = baseData.CashAccountId.ToString(),
			["__RequestVerificationToken"] = token
		});

		return await client.PostAsync("/EmployeeAdvance/Create", form);
	}
}