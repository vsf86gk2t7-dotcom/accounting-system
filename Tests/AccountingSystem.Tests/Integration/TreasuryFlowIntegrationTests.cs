using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class TreasuryFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public TreasuryFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"treasury.view",
			"treasury.receive",
			"treasury.pay",
			"treasury.adjust",
			"treasury.account.manage")
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

		var response = await client.GetAsync("/Treasury/Index");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// =========================================
	// 2. Receive — Save
	// =========================================

	[Fact]
	public async Task Receive_Post_PersistsTreasuryTransaction()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var amount = 250m;
		var transactionId = await TreasuryTestHelpers
			.ReceiveViaHttpAsync(_factory, client, baseData, amount: amount);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var transaction = await db.TreasuryTransactions.FindAsync(transactionId);

		transaction.Should().NotBeNull();
		transaction!.Type.Should().Be(TreasuryTransactionType.Receive);
		transaction.Amount.Should().Be(amount);
		transaction.CashAccountId.Should().Be(baseData.CashAccountId);
		transaction.CustomerId.Should().Be(baseData.CustomerId);
	}

	// =========================================
	// 3. Receive — Invalid Account
	// =========================================

	[Fact]
	public async Task Receive_WithInvalidAccount_ReturnsViewWithError()
	{
		await TreasuryTestHelpers.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Receive");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = "999999",
			["Amount"] = "100",
			["CustomerId"] = "1",
			["Reason"] = "اختبار",
			["ApplyCommission"] = "false",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Receive", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.TreasuryTransactions
			.AnyAsync(x =>
				x.Type == TreasuryTransactionType.Receive &&
				x.CashAccountId == 999999);

		any.Should().BeFalse();
	}

	// =========================================
	// 4. Receive — Without Party
	// =========================================

	[Fact]
	public async Task Receive_WithoutParty_ReturnsViewWithError()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Receive");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = "100",
			["CustomerId"] = string.Empty,
			["SupplierId"] = string.Empty,
			["EmployeeId"] = string.Empty,
			["OtherIncomeAccountId"] = string.Empty,
			["Reason"] = "بدون طرف",
			["ApplyCommission"] = "false",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Receive", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.TreasuryTransactions
			.AnyAsync(x =>
				x.Type == TreasuryTransactionType.Receive &&
				x.Reason == "بدون طرف");

		any.Should().BeFalse();
	}

	// =========================================
	// 5. Receive — Zero Amount
	// =========================================

	[Fact]
	public async Task Receive_WithZeroAmount_ReturnsViewWithError()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Receive");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = "0",
			["CustomerId"] = baseData.CustomerId.ToString(),
			["Reason"] = "صفر",
			["ApplyCommission"] = "false",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Receive", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.TreasuryTransactions
			.AnyAsync(x =>
				x.Type == TreasuryTransactionType.Receive &&
				x.Amount == 0);

		any.Should().BeFalse();
	}

	// =========================================
	// 6. Transactions list
	// =========================================

	[Fact]
	public async Task Transactions_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Treasury/Transactions");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// =========================================
	// 7. Pay — Valid
	// =========================================

	[Fact]
	public async Task Pay_Post_PersistsTreasuryTransaction()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		// 1. لازم رصيد أول — اعمل Receive بـ 500
		await TreasuryTestHelpers
			.ReceiveViaHttpAsync(_factory, client, baseData, amount: 500);

		// 2. Pay بـ 100
		var payAmount = 100m;
		var payId = await TreasuryTestHelpers
			.PayViaHttpAsync(_factory, client, baseData, amount: payAmount);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var transaction = await db.TreasuryTransactions.FindAsync(payId);

		transaction.Should().NotBeNull();
		transaction!.Type.Should().Be(TreasuryTransactionType.Pay);
		transaction.Amount.Should().Be(payAmount);
		transaction.CashAccountId.Should().Be(baseData.CashAccountId);
	}

	// =========================================
	// 8. Pay — Insufficient Balance
	// =========================================

	[Fact]
	public async Task Pay_WithInsufficientBalance_ReturnsViewWithError()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		// 1. Receive بـ 50
		await TreasuryTestHelpers
			.ReceiveViaHttpAsync(_factory, client, baseData, amount: 50);

		// 2. محاولة Pay بـ 200
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Pay");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = "200",
			["CustomerId"] = string.Empty,
			["SupplierId"] = string.Empty,
			["EmployeeId"] = string.Empty,
			["ExpenseAccountId"] = baseData.ExpenseAccountId.ToString(),
			["Reason"] = "رصيد غير كافي",
			["WalletFee"] = string.Empty,
			["RecipientWalletProvider"] = string.Empty,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Pay", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.TreasuryTransactions
			.AnyAsync(x =>
				x.Type == TreasuryTransactionType.Pay &&
				x.Reason == "رصيد غير كافي");

		any.Should().BeFalse();
	}

	// =========================================
	// 9. Pay — Zero Amount
	// =========================================

	[Fact]
	public async Task Pay_WithZeroAmount_ReturnsViewWithError()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		// Receive أول
		await TreasuryTestHelpers
			.ReceiveViaHttpAsync(_factory, client, baseData, amount: 500);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Pay");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = "0",
			["ExpenseAccountId"] = baseData.ExpenseAccountId.ToString(),
			["Reason"] = "صفر",
			["WalletFee"] = string.Empty,
			["RecipientWalletProvider"] = string.Empty,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Pay", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.TreasuryTransactions
			.AnyAsync(x =>
				x.Type == TreasuryTransactionType.Pay &&
				x.Amount == 0);

		any.Should().BeFalse();
	}

	// =========================================
	// 10. Adjust — Increase
	// =========================================

	[Fact]
	public async Task Adjust_Post_Increase_PersistsPositiveAmount()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var amount = 250m;
		var adjustId = await TreasuryTestHelpers
			.AdjustViaHttpAsync(
				_factory, client, baseData,
				amount: amount, isIncrease: true);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var transaction = await db.TreasuryTransactions.FindAsync(adjustId);

		transaction.Should().NotBeNull();
		transaction!.Type.Should().Be(TreasuryTransactionType.Adjust);
		transaction.Amount.Should().Be(amount);
		transaction.CashAccountId.Should().Be(baseData.CashAccountId);
	}

	// =========================================
	// 11. Adjust — Decrease
	// =========================================

	[Fact]
	public async Task Adjust_Post_Decrease_PersistsNegativeAmount()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		// Receive أول عشان الرصيد
		await TreasuryTestHelpers
			.ReceiveViaHttpAsync(_factory, client, baseData, amount: 500);

		var amount = 150m;
		var adjustId = await TreasuryTestHelpers
			.AdjustViaHttpAsync(
				_factory, client, baseData,
				amount: amount, isIncrease: false);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var transaction = await db.TreasuryTransactions.FindAsync(adjustId);

		transaction.Should().NotBeNull();
		transaction!.Type.Should().Be(TreasuryTransactionType.Adjust);
		transaction.Amount.Should().Be(-amount);
	}

	// =========================================
	// 12. Adjust — Decrease more than balance
	// =========================================

	[Fact]
	public async Task Adjust_Decrease_MoreThanBalance_ReturnsViewWithError()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		// Receive بـ 100
		await TreasuryTestHelpers
			.ReceiveViaHttpAsync(_factory, client, baseData, amount: 100);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Adjust");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = "500",
			["IsIncrease"] = "false",
			["Reason"] = "تسوية أكبر من الرصيد",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Adjust", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.TreasuryTransactions
			.AnyAsync(x =>
				x.Type == TreasuryTransactionType.Adjust &&
				x.Reason == "تسوية أكبر من الرصيد");

		any.Should().BeFalse();
	}

	// =========================================
	// 13. Adjust — Without Reason
	// =========================================

	[Fact]
	public async Task Adjust_WithoutReason_ReturnsViewWithError()
	{
		var baseData = await TreasuryTestHelpers
			.SeedTreasuryBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Treasury/Adjust");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["CashAccountId"] = baseData.CashAccountId.ToString(),
			["Amount"] = "100",
			["IsIncrease"] = "true",
			["Reason"] = "",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Treasury/Adjust", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.TreasuryTransactions
			.AnyAsync(x =>
				x.Type == TreasuryTransactionType.Adjust &&
				string.IsNullOrEmpty(x.Reason));

		any.Should().BeFalse();
	}
}