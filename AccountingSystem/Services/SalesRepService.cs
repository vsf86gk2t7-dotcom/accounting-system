using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Services
{
	public interface ISalesRepService
	{
		// يرجع SalesRepProfile.Id للمستخدم الحالي لو كان مندوبًا فعّالًا، وإلا null
		Task<int?> GetCurrentRepIdAsync();

		// يرجع بروفايل المندوب الكامل للمستخدم الحالي
		Task<SalesRepProfile?> GetCurrentRepProfileAsync();

		// إجمالي مبيعات اليوم للمندوب الحالي
		Task<decimal> GetTodaySalesAsync();

		// إجمالي مبيعات الشهر الحالي للمندوب
		Task<decimal> GetMonthSalesAsync();

		// رصيد مخزن العهدة (قيمة البضاعة بالمخزن)
		Task<decimal> GetCustodyBalanceAsync();

		// رصيد الخزينة النقدية للمندوب
		Task<decimal> GetCashBalanceAsync();

		// العمولة المكتسبة هذا الشهر حسب نوع العمولة
		Task<decimal> GetCommissionEarnedAsync();

		// آخر الفواتير للمندوب الحالي
		Task<List<SalesInvoice>> GetRecentInvoicesAsync(int count);

		// العملاء المرتبطين بالمندوب الحالي
		Task<List<Customer>> GetMyCustomersAsync();

		// التحقق من أن الخزينة تخص المندوب الحالي
		Task<bool> ValidateCashAccountOwnership(int cashAccountId);

		// التحقق من أن المنتج موجود في مخزن العهدة
		Task<bool> ValidateProductInCustody(int productId, int storeId);
	}

	public class SalesRepService : ISalesRepService
	{
		private const string CacheKey = "__AccountingSystem.SalesRepId__";

		private readonly ApplicationDbContext _context;
		private readonly IHttpContextAccessor _accessor;

		public SalesRepService(
			ApplicationDbContext context,
			IHttpContextAccessor accessor)
		{
			_context = context;
			_accessor = accessor;
		}

		public async Task<int?> GetCurrentRepIdAsync()
		{
			var http = _accessor.HttpContext;
			if (http == null) return null;

			// 0 = تم الفحص ومفيش مندوب
			if (http.Items[CacheKey] is int cached)
				return cached == 0 ? null : cached;

			int? repId = null;

			var idValue = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (int.TryParse(idValue, out var userId))
			{
				repId = await _context.SalesRepProfiles
					.AsNoTracking()
					.Where(p => p.IsActive &&
						_context.Users.Any(u =>
							u.Id == userId &&
							u.EmployeeId == p.EmployeeId))
					.Select(p => (int?)p.Id)
					.FirstOrDefaultAsync();
			}

			http.Items[CacheKey] = repId ?? 0;
			return repId;
		}

		public async Task<SalesRepProfile?> GetCurrentRepProfileAsync()
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return null;

			return await _context.SalesRepProfiles
				.AsNoTracking()
				.Include(p => p.Employee)
				.Include(p => p.CustodyStore)
				.Include(p => p.CashAccount)
				.FirstOrDefaultAsync(p => p.Id == repId.Value);
		}

		public async Task<decimal> GetTodaySalesAsync()
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return 0m;

			var today = DateTime.UtcNow.Date;
			var tomorrow = today.AddDays(1);

			return await _context.SalesInvoices
				.AsNoTracking()
				.Where(i => i.SalesRepId == repId.Value &&
							i.Status == SalesInvoiceStatus.Confirmed &&
							i.InvoiceDate >= today &&
							i.InvoiceDate < tomorrow)
				.SumAsync(i => i.TotalAmount);
		}

		public async Task<decimal> GetMonthSalesAsync()
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return 0m;

			var now = DateTime.UtcNow;
			var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
			var startOfNextMonth = startOfMonth.AddMonths(1);

			return await _context.SalesInvoices
				.AsNoTracking()
				.Where(i => i.SalesRepId == repId.Value &&
							i.Status == SalesInvoiceStatus.Confirmed &&
							i.InvoiceDate >= startOfMonth &&
							i.InvoiceDate < startOfNextMonth)
				.SumAsync(i => i.TotalAmount);
		}

		public async Task<decimal> GetCustodyBalanceAsync()
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return 0m;

			var profile = await _context.SalesRepProfiles
				.AsNoTracking()
				.Where(p => p.Id == repId.Value)
				.Select(p => new { p.CustodyStoreId })
				.FirstOrDefaultAsync();

			if (profile?.CustodyStoreId == null) return 0m;

			var storeId = profile.CustodyStoreId.Value;

			var balance = await _context.StockLots
				.AsNoTracking()
				.Where(l => l.StoreId == storeId && l.IsActive && l.QuantityRemaining > 0)
				.SumAsync(l => l.QuantityRemaining * l.UnitCost);

			return balance;
		}

		public async Task<decimal> GetCashBalanceAsync()
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return 0m;

			var profile = await _context.SalesRepProfiles
				.AsNoTracking()
				.Where(p => p.Id == repId.Value)
				.Select(p => new { p.CashAccountId })
				.FirstOrDefaultAsync();

			if (profile?.CashAccountId == null) return 0m;

			var accountId = profile.CashAccountId.Value;

			var outgoing = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x => x.CashAccountId == accountId)
				.SumAsync(x => x.Type == TreasuryTransactionType.Receive || x.Type == TreasuryTransactionType.Adjust
					? x.Amount
					: x.Type == TreasuryTransactionType.Pay || x.Type == TreasuryTransactionType.Transfer || x.Type == TreasuryTransactionType.WalletTransfer
						? -x.Amount
						: 0m);

			var incoming = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x => x.TransferToCashAccountId == accountId)
				.SumAsync(x => x.Amount);

			return outgoing + incoming;
		}

		public async Task<decimal> GetCommissionEarnedAsync()
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return 0m;

			var profile = await _context.SalesRepProfiles
				.AsNoTracking()
				.Where(p => p.Id == repId.Value)
				.Select(p => new { p.CommissionType, p.CommissionRate })
				.FirstOrDefaultAsync();

			if (profile == null) return 0m;

			var now = DateTime.UtcNow;
			var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
			var startOfNextMonth = startOfMonth.AddMonths(1);

			var confirmedInvoices = await _context.SalesInvoices
				.AsNoTracking()
				.Where(i => i.SalesRepId == repId.Value &&
							i.Status == SalesInvoiceStatus.Confirmed &&
							i.InvoiceDate >= startOfMonth &&
							i.InvoiceDate < startOfNextMonth)
				.ToListAsync();

			return profile.CommissionType switch
			{
				CommissionType.PercentOfSales => confirmedInvoices.Sum(i => i.TotalAmount) * profile.CommissionRate / 100m,

				CommissionType.FixedPerInvoice => confirmedInvoices.Count * profile.CommissionRate,

				CommissionType.PercentOfCollections => await GetCollectionsCommissionAsync(repId.Value, startOfMonth, startOfNextMonth, profile.CommissionRate),

				_ => 0m
			};
		}

		private async Task<decimal> GetCollectionsCommissionAsync(int repId, DateTime startOfMonth, DateTime startOfNextMonth, decimal rate)
		{
			var invoiceIds = await _context.SalesInvoices
				.AsNoTracking()
				.Where(i => i.SalesRepId == repId &&
							i.Status == SalesInvoiceStatus.Confirmed &&
							i.InvoiceDate >= startOfMonth &&
							i.InvoiceDate < startOfNextMonth)
				.Select(i => i.Id)
				.ToListAsync();

			if (invoiceIds.Count == 0) return 0m;

			var collected = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(t => t.CustomerId.HasValue &&
							t.Type == TreasuryTransactionType.Receive &&
							t.CreatedAt >= startOfMonth &&
							t.CreatedAt < startOfNextMonth)
				.SumAsync(t => t.Amount);

			return collected * rate / 100m;
		}

		public async Task<List<SalesInvoice>> GetRecentInvoicesAsync(int count)
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return new List<SalesInvoice>();

			return await _context.SalesInvoices
				.AsNoTracking()
				.Include(i => i.Customer)
				.Include(i => i.Items)
				.Where(i => i.SalesRepId == repId.Value)
				.OrderByDescending(i => i.InvoiceDate)
				.ThenByDescending(i => i.Id)
				.Take(count)
				.ToListAsync();
		}

		public async Task<List<Customer>> GetMyCustomersAsync()
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return new List<Customer>();

			return await _context.Customers
				.AsNoTracking()
				.Where(c => c.SalesRepId == repId.Value && c.IsActive)
				.OrderBy(c => c.Name)
				.ToListAsync();
		}

		public async Task<bool> ValidateCashAccountOwnership(int cashAccountId)
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return false;

			return await _context.SalesRepProfiles
				.AsNoTracking()
				.AnyAsync(p => p.Id == repId.Value && p.CashAccountId == cashAccountId);
		}

		public async Task<bool> ValidateProductInCustody(int productId, int storeId)
		{
			var repId = await GetCurrentRepIdAsync();
			if (repId == null) return false;

			var profile = await _context.SalesRepProfiles
				.AsNoTracking()
				.Where(p => p.Id == repId.Value)
				.Select(p => new { p.CustodyStoreId })
				.FirstOrDefaultAsync();

			if (profile?.CustodyStoreId == null) return false;

			if (profile.CustodyStoreId.Value != storeId) return false;

			return await _context.StockLots
				.AsNoTracking()
				.AnyAsync(l => l.StoreId == storeId &&
							  l.ProductId == productId &&
							  l.IsActive &&
							  l.QuantityRemaining > 0);
		}
	}
}
