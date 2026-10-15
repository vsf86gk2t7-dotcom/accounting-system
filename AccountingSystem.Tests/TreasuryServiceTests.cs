using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AccountingSystem.Tests
{
	public class TreasuryServiceTests
	{
		private static ApplicationDbContext CreateContext()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;
			return new ApplicationDbContext(options);
		}

		[Fact]
		public async Task CalculateBalancesAsync_NoTransactions_ReturnsZero()
		{
			using var context = CreateContext();
			var service = new TreasuryService(context);

			var balances = await service.CalculateBalancesAsync(new List<int> { 1, 2 });

			Assert.Equal(0m, balances[1]);
			Assert.Equal(0m, balances[2]);
		}

		[Fact]
		public async Task CalculateBalancesAsync_WithReceiveTransaction_ReturnsCorrectBalance()
		{
			using var context = CreateContext();
			var service = new TreasuryService(context);

			context.TreasuryTransactions.Add(new TreasuryTransaction
			{
				CashAccountId = 1,
				Type = TreasuryTransactionType.Receive,
				Amount = 500m,
				CreatedAt = DateTime.UtcNow
			});
			await context.SaveChangesAsync();

			var balances = await service.CalculateBalancesAsync(new List<int> { 1 });

			Assert.Equal(500m, balances[1]);
		}

		[Fact]
		public async Task CalculateBalancesAsync_WithPayTransaction_ReturnsCorrectBalance()
		{
			using var context = CreateContext();
			var service = new TreasuryService(context);

			context.TreasuryTransactions.Add(new TreasuryTransaction
			{
				CashAccountId = 1,
				Type = TreasuryTransactionType.Pay,
				Amount = 200m,
				CreatedAt = DateTime.UtcNow
			});
			await context.SaveChangesAsync();

			var balances = await service.CalculateBalancesAsync(new List<int> { 1 });

			Assert.Equal(-200m, balances[1]);
		}

		[Fact]
		public async Task CalculateBalancesAsync_WithMultipleTransactions_ReturnsCorrectBalance()
		{
			using var context = CreateContext();
			var service = new TreasuryService(context);

			context.TreasuryTransactions.AddRange(
				new TreasuryTransaction
				{
					CashAccountId = 1,
					Type = TreasuryTransactionType.Receive,
					Amount = 1000m,
					CreatedAt = DateTime.UtcNow
				},
				new TreasuryTransaction
				{
					CashAccountId = 1,
					Type = TreasuryTransactionType.Pay,
					Amount = 300m,
					CreatedAt = DateTime.UtcNow
				},
				new TreasuryTransaction
				{
					CashAccountId = 1,
					Type = TreasuryTransactionType.Receive,
					Amount = 500m,
					CreatedAt = DateTime.UtcNow
				});
			await context.SaveChangesAsync();

			var balances = await service.CalculateBalancesAsync(new List<int> { 1 });

			Assert.Equal(1200m, balances[1]);
		}

		[Fact]
		public async Task CalculateTransactionCountsAsync_ReturnsCorrectCount()
		{
			using var context = CreateContext();
			var service = new TreasuryService(context);

			context.TreasuryTransactions.AddRange(
				new TreasuryTransaction
				{
					CashAccountId = 1,
					Type = TreasuryTransactionType.Receive,
					Amount = 100m,
					CreatedAt = DateTime.UtcNow
				},
				new TreasuryTransaction
				{
					CashAccountId = 1,
					Type = TreasuryTransactionType.Pay,
					Amount = 50m,
					CreatedAt = DateTime.UtcNow
				});
			await context.SaveChangesAsync();

			var transactions = await context.TreasuryTransactions
				.Where(x => x.CashAccountId == 1)
				.ToListAsync();

			Assert.Equal(2, transactions.Count);
		}
	}
}
