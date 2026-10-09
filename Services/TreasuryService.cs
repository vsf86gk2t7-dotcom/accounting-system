using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
	public interface ITreasuryService
	{
		Task<Dictionary<int, decimal>> CalculateBalancesAsync(List<int> accountIds);
		Task<Dictionary<int, int>> CalculateTransactionCountsAsync(List<int> accountIds);
		Task<List<TreasuryTransaction>> GetTransactionsAsync(
			List<int> accountIds, int page, int pageSize);
		Task<int> GetTransactionCountAsync(List<int> accountIds);
	}

	public class TreasuryService : ITreasuryService
	{
		private readonly ApplicationDbContext _context;

		public TreasuryService(ApplicationDbContext context)
		{
			_context = context;
		}

	public async Task<Dictionary<int, decimal>> CalculateBalancesAsync(List<int> accountIds)
	{
		var accountIdsSet = accountIds.ToHashSet();

		var outgoing = await _context.TreasuryTransactions
			.AsNoTracking()
			.Where(x => accountIdsSet.Contains(x.CashAccountId))
			.GroupBy(x => x.CashAccountId)
			.Select(g => new
			{
				AccountId = g.Key,
				Balance = g.Sum(x => x.Type == TreasuryTransactionType.Receive || x.Type == TreasuryTransactionType.Adjust
					? x.Amount
					: x.Type == TreasuryTransactionType.Pay || x.Type == TreasuryTransactionType.Transfer || x.Type == TreasuryTransactionType.WalletTransfer
						? -x.Amount
						: 0m)
			})
			.ToDictionaryAsync(x => x.AccountId, x => x.Balance);

		var incoming = await _context.TreasuryTransactions
			.AsNoTracking()
			.Where(x => x.TransferToCashAccountId.HasValue &&
						accountIdsSet.Contains(x.TransferToCashAccountId.Value))
			.GroupBy(x => x.TransferToCashAccountId!.Value)
			.Select(g => new
			{
				AccountId = g.Key,
				Balance = g.Sum(x => x.Amount)
			})
			.ToDictionaryAsync(x => x.AccountId, x => x.Balance);

		var balances = new Dictionary<int, decimal>();

		foreach (var accountId in accountIds)
		{
			decimal balance = 0;
			if (outgoing.TryGetValue(accountId, out var outBalance))
				balance += outBalance;
			if (incoming.TryGetValue(accountId, out var inBalance))
				balance += inBalance;
			balances[accountId] = balance;
		}

		return balances;
	}

								public async Task<Dictionary<int, int>> CalculateTransactionCountsAsync(List<int> accountIds)
		{
			// ✅ (EF InMemory-compatible) نجيب البيانات الأول
			// وبعدين نحسب الـ counts في الذاكرة
			var transactions = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x => accountIds.Contains(x.CashAccountId) ||
						(x.TransferToCashAccountId.HasValue && accountIds.Contains(x.TransferToCashAccountId.Value)))
				.Select(x => new
				{
					x.CashAccountId,
					x.TransferToCashAccountId
				})
				.ToListAsync();

			var result = new Dictionary<int, int>();
			foreach (var accountId in accountIds)
			{
				result[accountId] = transactions.Count(x =>
					x.CashAccountId == accountId ||
					(x.TransferToCashAccountId.HasValue &&
					 x.TransferToCashAccountId.Value == accountId));
			}
			return result;
		}
		public async Task<List<TreasuryTransaction>> GetTransactionsAsync(
			List<int> accountIds, int page, int pageSize)
		{
			return await _context.TreasuryTransactions
				.AsNoTracking()
				.Include(x => x.CashAccount)
				.Include(x => x.TransferToCashAccount)
				.Include(x => x.CreatedByUser)
				.Where(x => accountIds.Contains(x.CashAccountId) ||
							(x.TransferToCashAccountId.HasValue && accountIds.Contains(x.TransferToCashAccountId.Value)))
				.OrderByDescending(x => x.CreatedAt)
				.ThenByDescending(x => x.Id)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();
		}

		public async Task<int> GetTransactionCountAsync(List<int> accountIds)
		{
			return await _context.TreasuryTransactions
				.AsNoTracking()
				.CountAsync(x => accountIds.Contains(x.CashAccountId) ||
								(x.TransferToCashAccountId.HasValue && accountIds.Contains(x.TransferToCashAccountId.Value)));
		}
	}
}
