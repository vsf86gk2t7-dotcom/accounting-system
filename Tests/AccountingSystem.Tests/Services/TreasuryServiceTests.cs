using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Tests.Services;

public class TreasuryServiceTests : BaseTest
{
	private readonly TreasuryService _sut;

	public TreasuryServiceTests()
	{
		_sut = new TreasuryService(Context);
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task<CashAccount> SeedCashAccountAsync(string name = "Test Account")
	{
		var account = new CashAccount
		{
			Name = name,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		Context.CashAccounts.Add(account);
		await Context.SaveChangesAsync();

		return account;
	}

	private async Task<TreasuryTransaction> SeedTransactionAsync(
		int cashAccountId,
		TreasuryTransactionType type,
		decimal amount,
		int? transferToCashAccountId = null,
		DateTime? createdAt = null)
	{
		var transaction = new TreasuryTransaction
		{
			TransactionNumber = Guid.NewGuid().ToString("N").Substring(0, 10),
			Type = type,
			CashAccountId = cashAccountId,
			Amount = amount,
			TransferToCashAccountId = transferToCashAccountId,
			CreatedAt = createdAt ?? DateTime.UtcNow,
			Reason = "Test"
		};

		Context.TreasuryTransactions.Add(transaction);
		await Context.SaveChangesAsync();

		return transaction;
	}

	// =========================================
	// 1. CalculateBalancesAsync — حالات الحدود
	// =========================================

	[Fact]
	public async Task CalculateBalancesAsync_WithEmptyList_ReturnsEmptyDictionary()
	{
		var result = await _sut.CalculateBalancesAsync(new List<int>());

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithNoTransactions_ReturnsZeroForEach()
	{
		var account1 = await SeedCashAccountAsync("A1");
		var account2 = await SeedCashAccountAsync("A2");

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account1.Id, account2.Id });

		result.Should().HaveCount(2);
		result[account1.Id].Should().Be(0m);
		result[account2.Id].Should().Be(0m);
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithSingleAccountNoTransactions_ReturnsZero()
	{
		var account = await SeedCashAccountAsync();

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account.Id });

		result[account.Id].Should().Be(0m);
	}

	// =========================================
	// 2. CalculateBalancesAsync — أنواع الحركات
	// =========================================

	[Fact]
	public async Task CalculateBalancesAsync_WithReceive_AddsToBalance()
	{
		var account = await SeedCashAccountAsync();
		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Receive, 500m);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account.Id });

		result[account.Id].Should().Be(500m);
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithPay_SubtractsFromBalance()
	{
		var account = await SeedCashAccountAsync();
		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Pay, 300m);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account.Id });

		result[account.Id].Should().Be(-300m);
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithAdjust_AddsToBalance()
	{
		var account = await SeedCashAccountAsync();
		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Adjust, 150m);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account.Id });

		result[account.Id].Should().Be(150m);
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithTransferOut_SubtractsFromBalance()
	{
		var account1 = await SeedCashAccountAsync("From");
		var account2 = await SeedCashAccountAsync("To");

		await SeedTransactionAsync(
			account1.Id,
			TreasuryTransactionType.Transfer,
			200m,
			transferToCashAccountId: account2.Id);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account1.Id });

		result[account1.Id].Should().Be(-200m);
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithTransferIn_AddsToBalance()
	{
		var account1 = await SeedCashAccountAsync("From");
		var account2 = await SeedCashAccountAsync("To");

		await SeedTransactionAsync(
			account1.Id,
			TreasuryTransactionType.Transfer,
			200m,
			transferToCashAccountId: account2.Id);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account2.Id });

		result[account2.Id].Should().Be(200m);
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithWalletTransfer_SubtractsFromBalance()
	{
		var account = await SeedCashAccountAsync();
		await SeedTransactionAsync(
			account.Id, TreasuryTransactionType.WalletTransfer, 100m);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account.Id });

		result[account.Id].Should().Be(-100m);
	}

	// =========================================
	// 3. CalculateBalancesAsync — سيناريوهات مركبة
	// =========================================

	[Fact]
	public async Task CalculateBalancesAsync_WithMixedTransactions_CalculatesCorrectly()
	{
		// Receive 1000 - Pay 300 + Adjust 50 - Transfer 100 = 650
		var account = await SeedCashAccountAsync();
		var target = await SeedCashAccountAsync("Target");

		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Receive, 1000m);
		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Pay, 300m);
		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Adjust, 50m);
		await SeedTransactionAsync(
			account.Id, TreasuryTransactionType.Transfer, 100m,
			transferToCashAccountId: target.Id);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account.Id });

		result[account.Id].Should().Be(650m);
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithMultipleAccounts_CalculatesEachIndependently()
	{
		var account1 = await SeedCashAccountAsync("A1");
		var account2 = await SeedCashAccountAsync("A2");

		await SeedTransactionAsync(account1.Id, TreasuryTransactionType.Receive, 1000m);
		await SeedTransactionAsync(account2.Id, TreasuryTransactionType.Receive, 500m);
		await SeedTransactionAsync(account2.Id, TreasuryTransactionType.Pay, 200m);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account1.Id, account2.Id });

		result[account1.Id].Should().Be(1000m);
		result[account2.Id].Should().Be(300m);
	}

	[Fact]
	public async Task CalculateBalancesAsync_IgnoresAccountsNotInList()
	{
		var account1 = await SeedCashAccountAsync("A1");
		var account2 = await SeedCashAccountAsync("A2");

		await SeedTransactionAsync(account1.Id, TreasuryTransactionType.Receive, 1000m);
		await SeedTransactionAsync(account2.Id, TreasuryTransactionType.Receive, 500m);

		// نطلب رصيد account1 بس
		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account1.Id });

		result.Should().HaveCount(1);
		result[account1.Id].Should().Be(1000m);
		result.Should().NotContainKey(account2.Id);
	}

	[Fact]
	public async Task CalculateBalancesAsync_WithTransferBothWays_CalculatesNet()
	{
		// account1 يستقبل 500، يُرسل 200 → 300
		var account1 = await SeedCashAccountAsync("A1");
		var account2 = await SeedCashAccountAsync("A2");

		// A2 → A1: 500
		await SeedTransactionAsync(
			account2.Id, TreasuryTransactionType.Transfer, 500m,
			transferToCashAccountId: account1.Id);

		// A1 → A2: 200
		await SeedTransactionAsync(
			account1.Id, TreasuryTransactionType.Transfer, 200m,
			transferToCashAccountId: account2.Id);

		var result = await _sut.CalculateBalancesAsync(
			new List<int> { account1.Id });

		result[account1.Id].Should().Be(300m);
	}

	// =========================================
	// 4. CalculateTransactionCountsAsync
	// =========================================

	[Fact]
	public async Task CalculateTransactionCountsAsync_WithEmptyList_ReturnsEmpty()
	{
		var result = await _sut.CalculateTransactionCountsAsync(new List<int>());

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task CalculateTransactionCountsAsync_WithNoTransactions_ReturnsZero()
	{
		var account = await SeedCashAccountAsync();

		var result = await _sut.CalculateTransactionCountsAsync(
			new List<int> { account.Id });

		result[account.Id].Should().Be(0);
	}

	[Fact]
	public async Task CalculateTransactionCountsAsync_CountsTransactions()
	{
		var account = await SeedCashAccountAsync();

		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Receive, 100m);
		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Pay, 50m);
		await SeedTransactionAsync(account.Id, TreasuryTransactionType.Adjust, 25m);

		var result = await _sut.CalculateTransactionCountsAsync(
			new List<int> { account.Id });

		result[account.Id].Should().Be(3);
	}

	// =========================================
	// 5. GetTransactionsAsync
	// =========================================

	[Fact]
	public async Task GetTransactionsAsync_WithNoTransactions_ReturnsEmpty()
	{
		var account = await SeedCashAccountAsync();

		var result = await _sut.GetTransactionsAsync(
			new List<int> { account.Id }, page: 1, pageSize: 10);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetTransactionsAsync_WithTransactions_ReturnsAll()
	{
		var account = await SeedCashAccountAsync();

		for (int i = 0; i < 5; i++)
		{
			await SeedTransactionAsync(
				account.Id, TreasuryTransactionType.Receive, 100m);
		}

		var result = await _sut.GetTransactionsAsync(
			new List<int> { account.Id }, page: 1, pageSize: 10);

		result.Should().HaveCount(5);
	}

	[Fact]
	public async Task GetTransactionsAsync_OrdersByCreatedAtDescending()
	{
		var account = await SeedCashAccountAsync();
		var baseDate = DateTime.UtcNow;

		var tx1 = await SeedTransactionAsync(
			account.Id, TreasuryTransactionType.Receive, 100m,
			createdAt: baseDate.AddDays(-2));
		var tx2 = await SeedTransactionAsync(
			account.Id, TreasuryTransactionType.Receive, 200m,
			createdAt: baseDate.AddDays(-1));
		var tx3 = await SeedTransactionAsync(
			account.Id, TreasuryTransactionType.Receive, 300m,
			createdAt: baseDate);

		var result = await _sut.GetTransactionsAsync(
			new List<int> { account.Id }, page: 1, pageSize: 10);

		result[0].Id.Should().Be(tx3.Id); // الأحدث
		result[1].Id.Should().Be(tx2.Id);
		result[2].Id.Should().Be(tx1.Id); // الأقدم
	}

	[Fact]
	public async Task GetTransactionsAsync_WithPagination_ReturnsCorrectPage()
	{
		var account = await SeedCashAccountAsync();

		for (int i = 0; i < 15; i++)
		{
			await SeedTransactionAsync(
				account.Id, TreasuryTransactionType.Receive, 100m);
		}

		var page1 = await _sut.GetTransactionsAsync(
			new List<int> { account.Id }, page: 1, pageSize: 10);
		var page2 = await _sut.GetTransactionsAsync(
			new List<int> { account.Id }, page: 2, pageSize: 10);

		page1.Should().HaveCount(10);
		page2.Should().HaveCount(5);

		// الصفحتين مش فيهم تداخل
		var page1Ids = page1.Select(x => x.Id).ToHashSet();
		var page2Ids = page2.Select(x => x.Id).ToHashSet();
		page1Ids.Intersect(page2Ids).Should().BeEmpty();
	}

	[Fact]
	public async Task GetTransactionsAsync_IncludesTransferToAccount()
	{
		var account1 = await SeedCashAccountAsync("A1");
		var account2 = await SeedCashAccountAsync("A2");

		await SeedTransactionAsync(
			account1.Id, TreasuryTransactionType.Transfer, 500m,
			transferToCashAccountId: account2.Id);

		var result = await _sut.GetTransactionsAsync(
			new List<int> { account1.Id }, page: 1, pageSize: 10);

		result.Should().HaveCount(1);
		result[0].TransferToCashAccount.Should().NotBeNull();
		result[0].TransferToCashAccount!.Id.Should().Be(account2.Id);
	}

	// =========================================
	// 6. GetTransactionCountAsync
	// =========================================

	[Fact]
	public async Task GetTransactionCountAsync_WithEmptyList_ReturnsZero()
	{
		var count = await _sut.GetTransactionCountAsync(new List<int>());

		count.Should().Be(0);
	}

	[Fact]
	public async Task GetTransactionCountAsync_WithNoTransactions_ReturnsZero()
	{
		var account = await SeedCashAccountAsync();

		var count = await _sut.GetTransactionCountAsync(
			new List<int> { account.Id });

		count.Should().Be(0);
	}

	[Fact]
	public async Task GetTransactionCountAsync_CountsAllTransactions()
	{
		var account = await SeedCashAccountAsync();

		for (int i = 0; i < 7; i++)
		{
			await SeedTransactionAsync(
				account.Id, TreasuryTransactionType.Receive, 100m);
		}

		var count = await _sut.GetTransactionCountAsync(
			new List<int> { account.Id });

		count.Should().Be(7);
	}
}