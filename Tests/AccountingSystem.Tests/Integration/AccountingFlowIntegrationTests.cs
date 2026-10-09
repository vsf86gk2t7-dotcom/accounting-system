using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class AccountingFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public AccountingFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"accounting.view",
			"accounting.manage",
			"accounting.journal.view",
			"accounting.journal.create",
			"accounting.journal.edit",
			"accounting.journal.approve")
			.GetAwaiter().GetResult();
	}

	// Helper: زرع ChartOfAccounts + FiscalPeriod
	private async Task<(int DebitAccountId, int CreditAccountId)> SeedChartAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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

		// هات حسابين — واحد Asset، واحد Revenue (أو Expense)
		var debit = await db.ChartAccounts
			.AsNoTracking()
			.Where(x => x.IsActive)
			.OrderBy(x => x.Id)
			.FirstAsync();

		var credit = await db.ChartAccounts
			.AsNoTracking()
			.Where(x => x.IsActive && x.Id != debit.Id)
			.OrderBy(x => x.Id)
			.FirstAsync();

		return (debit.Id, credit.Id);
	}

	// Helper: إنشاء قيد بسيط
	private async Task<int> CreateEntryAsync(
		HttpClient client,
		int debitId,
		int creditId,
		decimal amount = 100,
		string description = "قيد اختبار")
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Accounting/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["EntryDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Description"] = description,
			["Lines[0].ChartAccountId"] = debitId.ToString(),
			["Lines[0].Debit"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
			["Lines[0].Credit"] = "0",
			["Lines[1].ChartAccountId"] = creditId.ToString(),
			["Lines[1].Debit"] = "0",
			["Lines[1].Credit"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Accounting/Create", form);

		if (response.StatusCode != HttpStatusCode.Redirect &&
			response.StatusCode != HttpStatusCode.Found &&
			response.StatusCode != HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateEntry failed. Status={response.StatusCode}. " +
				$"Body={body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var entry = await db.JournalEntries
			.OrderByDescending(x => x.Id)
			.FirstAsync();

		return entry.Id;
	}

	// 1. Accounts
	[Fact]
	public async Task Accounts_Returns200()
	{
		await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Accounting/Accounts");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Journal GET
	[Fact]
	public async Task Journal_Returns200()
	{
		await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Accounting/Journal");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Accounting/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 4. Create POST — قيد متوازن
	[Fact]
	public async Task Create_Post_BalancedEntry_PersistsJournalEntry()
	{
		var (debitId, creditId) = await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Accounting/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["EntryDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Description"] = "قيد اختبار متوازن",

			["Lines[0].ChartAccountId"] = debitId.ToString(),
			["Lines[0].Debit"] = "1000",
			["Lines[0].Credit"] = "0",

			["Lines[1].ChartAccountId"] = creditId.ToString(),
			["Lines[1].Debit"] = "0",
			["Lines[1].Credit"] = "1000",

			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Accounting/Create", form);

		var body = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther },
			$"Expected redirect, got {response.StatusCode}. Body: {body.Substring(0, Math.Min(500, body.Length))}");

		// تأكد إن القيد اتحفظ
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var entry = await db.JournalEntries
			.Include(x => x.Lines)
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		entry.Should().NotBeNull();
		entry!.Lines.Should().HaveCount(2);
		entry.Lines.Sum(x => x.Debit).Should().Be(1000);
		entry.Lines.Sum(x => x.Credit).Should().Be(1000);
	}

	// 5. Create POST — قيد غير متوازن
	[Fact]
	public async Task Create_Post_UnbalancedEntry_ReturnsViewWithError()
	{
		var (debitId, creditId) = await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Accounting/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["EntryDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Description"] = "قيد غير متوازن",

			["Lines[0].ChartAccountId"] = debitId.ToString(),
			["Lines[0].Debit"] = "1000",
			["Lines[0].Credit"] = "0",

			["Lines[1].ChartAccountId"] = creditId.ToString(),
			["Lines[1].Debit"] = "0",
			["Lines[1].Credit"] = "500",  // ⬅️ غير متوازن

			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Accounting/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// تأكد مفيش قيد اتحفظ
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.JournalEntries
			.AnyAsync(x => x.Description == "قيد غير متوازن");

		any.Should().BeFalse();
	}

	// 6. Create POST — أقل من سطرين
	[Fact]
	public async Task Create_Post_LessThanTwoLines_ReturnsViewWithError()
	{
		var (debitId, _) = await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Accounting/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["EntryDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Description"] = "سطر واحد بس",

			["Lines[0].ChartAccountId"] = debitId.ToString(),
			["Lines[0].Debit"] = "1000",
			["Lines[0].Credit"] = "0",

			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Accounting/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.JournalEntries
			.AnyAsync(x => x.Description == "سطر واحد بس");

		any.Should().BeFalse();
	}

	// 7. Edit GET — قيد مسودة
	[Fact]
	public async Task Edit_Get_DraftEntry_Returns200()
	{
		var (debitId, creditId) = await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var entryId = await CreateEntryAsync(
			client, debitId, creditId,
			amount: 100, description: "قيد للتعديل");

		var response = await client.GetAsync($"/Accounting/Edit/{entryId}");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 8. Edit GET — قيد معتمد (لا يمكن تعديله)
	[Fact]
	public async Task Edit_Get_PostedEntry_Redirects()
	{
		var (debitId, creditId) = await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var entryId = await CreateEntryAsync(
			client, debitId, creditId,
			amount: 50, description: "قيد معتمد");

		// اعتمد القيد
		var approveToken = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Accounting/Journal");

		var approveForm = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = approveToken
		});

		await client.PostAsync($"/Accounting/Approve/{entryId}", approveForm);

		// جرّب Edit → redirect
		var response = await client.GetAsync($"/Accounting/Edit/{entryId}");

		response.StatusCode.Should().BeOneOf(
			HttpStatusCode.Redirect,
			HttpStatusCode.Found,
			HttpStatusCode.SeeOther);
	}

	// 9. Edit GET — قيد مش موجود
	[Fact]
	public async Task Edit_Get_NonExisting_ReturnsNotFound()
	{
		await SeedChartAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Accounting/Edit/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}