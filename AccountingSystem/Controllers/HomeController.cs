using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Admin;
using AccountingSystem.Models.ViewModels.Dashboard;
using AccountingSystem.Services.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class HomeController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IWebHostEnvironment _environment;
		private readonly IPermissionService _permissionService;

		private class DashboardCounts
		{
			public int Companies { get; set; }
			public int Branches { get; set; }
			public int Stores { get; set; }
			public int Products { get; set; }
			public int Categories { get; set; }
			public int Units { get; set; }
			public int Customers { get; set; }
			public int Suppliers { get; set; }
			public int Employees { get; set; }
			public int Users { get; set; }
			public int SalesInvoices { get; set; }
			public int PurchaseInvoices { get; set; }
		}

		public HomeController(
			ApplicationDbContext context,
			IWebHostEnvironment environment,
			IPermissionService permissionService)
		{
			_context = context;
			_environment = environment;
			_permissionService = permissionService;
		}

		[Authorize(Roles = "Admin,Employee")]
		public async Task<IActionResult> Index(string? range)
		{
			// =========================================
			// الفترة المختارة + الفترة السابقة المماثلة
			// =========================================

			var today = DateTime.Today;
			var (rangeKey, rangeLabel, periodFrom) = ResolveRange(range, today);
			var to = today.AddDays(1); // نهاية حصرية
			var span = to - periodFrom;
			var prevFrom = periodFrom - span;
			var prevTo = periodFrom;

			var model = new DashboardViewModel
			{
				Range = rangeKey,
				RangeLabel = rangeLabel,
				From = periodFrom,
				To = to.AddDays(-1)
			};

			// =========================================
			// الصلاحيات
			// =========================================

			model.IsAdmin = User.IsInRole("Admin");

			if (!model.IsAdmin &&
				int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
			{
				model.Permissions = await _permissionService.GetUserPermissionsAsync(currentUserId);
			}

			model.CanSales = model.Can("sales.view");
			model.CanPurchases = model.Can("purchase.view");
			model.CanAccounting = model.Can("accounting.view");
			model.CanTreasury = model.Can("treasury.view");
			model.CanCustomers = model.Can("customer.view");
			model.CanSuppliers = model.Can("supplier.view");
			model.CanInventory = model.Can("inventory.view");

			// =========================================
			// الأعداد الأساسية — استعلام واحد
			// =========================================

			var counts = _context.Database
				.SqlQueryRaw<DashboardCounts>(@"
					SELECT
						(SELECT COUNT(*) FROM [Companies]) AS Companies,
						(SELECT COUNT(*) FROM [Branches]) AS Branches,
						(SELECT COUNT(*) FROM [Stores]) AS Stores,
						(SELECT COUNT(*) FROM [Products]) AS Products,
						(SELECT COUNT(*) FROM [Categories]) AS Categories,
						(SELECT COUNT(*) FROM [Units]) AS Units,
						(SELECT COUNT(*) FROM [Customers]) AS Customers,
						(SELECT COUNT(*) FROM [Suppliers]) AS Suppliers,
						(SELECT COUNT(*) FROM [Employees]) AS Employees,
						(SELECT COUNT(*) FROM [Users]) AS Users,
						(SELECT COUNT(*) FROM [SalesInvoices]) AS SalesInvoices,
						(SELECT COUNT(*) FROM [PurchaseInvoices]) AS PurchaseInvoices")
				.Single();

			model.Companies = counts.Companies;
			model.Branches = counts.Branches;
			model.Stores = counts.Stores;
			model.Products = counts.Products;
			model.Categories = counts.Categories;
			model.Units = counts.Units;
			model.Customers = counts.Customers;
			model.Suppliers = counts.Suppliers;
			model.Employees = counts.Employees;
			model.Users = counts.Users;
			model.SalesInvoices = counts.SalesInvoices;
			model.PurchaseInvoices = counts.PurchaseInvoices;
			model.ConfirmedSales = await _context.SalesInvoices
	.CountAsync(x => x.Status == SalesInvoiceStatus.Confirmed);

			model.PostedPurchases = await _context.PurchaseInvoices
				.CountAsync(x => x.Status == PurchaseInvoiceStatus.Posted);

			// =========================================
			// المبيعات
			// =========================================

			var salesByDay = new Dictionary<DateTime, decimal>();
			var purchasesByDay = new Dictionary<DateTime, decimal>();

			var chartFrom = periodFrom < today.AddDays(-6) ? periodFrom : today.AddDays(-6);

			if (model.CanSales)
			{
				var confirmed = _context.SalesInvoices
					.AsNoTracking()
					.Where(x => x.Status == SalesInvoiceStatus.Confirmed);

				var inRange = confirmed.Where(x =>
					x.InvoiceDate >= periodFrom && x.InvoiceDate < to);

				model.SalesTotal = await inRange
					.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

				model.SalesCount = await inRange.CountAsync();

				model.SalesPrevious = await confirmed
					.Where(x => x.InvoiceDate >= prevFrom && x.InvoiceDate < prevTo)
					.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

				var dailySales = await confirmed
					.Where(x => x.InvoiceDate >= chartFrom && x.InvoiceDate < to)
					.GroupBy(x => x.InvoiceDate.Date)
					.Select(g => new { Day = g.Key, Total = g.Sum(x => x.TotalAmount) })
					.ToListAsync();

				foreach (var d in dailySales)
					salesByDay[d.Day] = d.Total;

				var methodNames = new Dictionary<SalesPaymentMethod, string>
				{
					[SalesPaymentMethod.Cash] = "نقدي",
					[SalesPaymentMethod.BankTransfer] = "تحويل بنكي",
					[SalesPaymentMethod.Cheque] = "شيك",
					[SalesPaymentMethod.Credit] = "آجل",
					[SalesPaymentMethod.Wallet] = "محفظة إلكترونية"
				};

				var mix = await inRange
					.GroupBy(x => x.PaymentMethod)
					.Select(g => new { Method = g.Key, Amount = g.Sum(x => x.TotalAmount) })
					.ToListAsync();

				model.PaymentMix = mix
					.Where(x => x.Amount > 0)
					.OrderByDescending(x => x.Amount)
					.Select(x => new NamedAmount
					{
						Name = methodNames.TryGetValue(x.Method, out var n) ? n : x.Method.ToString(),
						Amount = x.Amount
					})
					.ToList();

				model.TopProducts = await _context.SalesInvoiceItems
					.AsNoTracking()
					.Where(i =>
						i.SalesInvoice!.Status == SalesInvoiceStatus.Confirmed &&
						i.SalesInvoice.InvoiceDate >= periodFrom &&
						i.SalesInvoice.InvoiceDate < to)
					.GroupBy(i => new { i.ProductId, i.Product!.Name })
					.Select(g => new NamedAmount
					{
						Id = g.Key.ProductId,
						Name = g.Key.Name,
						Amount = g.Sum(i => i.TotalPrice)
					})
					.OrderByDescending(x => x.Amount)
					.Take(5)
					.ToListAsync();

				model.TopCustomers = await inRange
					.Where(x => x.CustomerId != null)
					.GroupBy(x => new { x.CustomerId, x.Customer!.Name })
					.Select(g => new NamedAmount
					{
						Id = g.Key.CustomerId,
						Name = g.Key.Name,
						Amount = g.Sum(x => x.TotalAmount)
					})
					.OrderByDescending(x => x.Amount)
					.Take(5)
					.ToListAsync();

				model.DraftSales = await _context.SalesInvoices
					.CountAsync(x => x.Status == SalesInvoiceStatus.Draft);

				model.OverdueSalesCount = await confirmed
					.CountAsync(x => x.DueDate != null && x.DueDate < today);

				model.OverdueSalesAmount = await confirmed
					.Where(x => x.DueDate != null && x.DueDate < today)
					.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

				model.UpcomingDueSalesCount = await confirmed
					.CountAsync(x => x.DueDate != null &&
									 x.DueDate >= today &&
									 x.DueDate < today.AddDays(8));

				model.RecentSales = await _context.SalesInvoices
					.AsNoTracking()
					.Include(x => x.Customer)
					.Where(x => x.Status == SalesInvoiceStatus.Confirmed)
					.OrderByDescending(x => x.InvoiceDate)
					.ThenByDescending(x => x.Id)
					.Take(6)
					.ToListAsync();
			}

			// =========================================
			// المشتريات
			// =========================================

			if (model.CanPurchases)
			{
				var posted = _context.PurchaseInvoices
					.AsNoTracking()
					.Where(x => x.Status == PurchaseInvoiceStatus.Posted);

				var inRange = posted.Where(x =>
					x.InvoiceDate >= periodFrom && x.InvoiceDate < to);

				model.PurchaseTotal = await inRange
					.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

				model.PurchaseCount = await inRange.CountAsync();

				model.PurchasePrevious = await posted
					.Where(x => x.InvoiceDate >= prevFrom && x.InvoiceDate < prevTo)
					.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

				var dailyPurchases = await posted
					.Where(x => x.InvoiceDate >= chartFrom && x.InvoiceDate < to)
					.GroupBy(x => x.InvoiceDate.Date)
					.Select(g => new { Day = g.Key, Total = g.Sum(x => x.TotalAmount) })
					.ToListAsync();

				foreach (var d in dailyPurchases)
					purchasesByDay[d.Day] = d.Total;

				model.DraftPurchases = await _context.PurchaseInvoices
					.CountAsync(x => x.Status == PurchaseInvoiceStatus.Draft);

				model.OverduePurchasesCount = await posted
					.CountAsync(x => x.DueDate != null && x.DueDate < today);

				model.OverduePurchasesAmount = await posted
					.Where(x => x.DueDate != null && x.DueDate < today)
					.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

				model.RecentPurchases = await _context.PurchaseInvoices
					.AsNoTracking()
					.Include(x => x.Supplier)
					.Where(x => x.Status == PurchaseInvoiceStatus.Posted)
					.OrderByDescending(x => x.InvoiceDate)
					.ThenByDescending(x => x.Id)
					.Take(6)
					.ToListAsync();
			}

			// =========================================
			// منحنى المبيعات والمشتريات
			// =========================================

			model.Trend = BuildTrend(chartFrom, today, salesByDay, purchasesByDay);

			// =========================================
			// صافي الربح
			// =========================================

			if (model.CanAccounting)
			{
				async Task<(decimal Revenue, decimal Expenses)> ProfitAsync(DateTime f, DateTime t)
				{
					var rows = await _context.JournalEntryLines
						.AsNoTracking()
						.Where(l =>
							l.JournalEntry!.IsPosted &&
							l.JournalEntry.EntryDate >= f &&
							l.JournalEntry.EntryDate < t &&
							(l.ChartAccount!.Type == AccountType.Revenue ||
							 l.ChartAccount.Type == AccountType.Expense))
						.GroupBy(l => l.ChartAccount!.Type)
						.Select(g => new
						{
							Type = g.Key,
							Debit = g.Sum(x => x.Debit),
							Credit = g.Sum(x => x.Credit)
						})
						.ToListAsync();

					var rev = rows.Where(r => r.Type == AccountType.Revenue).Sum(r => r.Credit - r.Debit);
					var exp = rows.Where(r => r.Type == AccountType.Expense).Sum(r => r.Debit - r.Credit);
					return (rev, exp);
				}

				var current = await ProfitAsync(periodFrom, to);
				var previous = await ProfitAsync(prevFrom, prevTo);

				model.Revenue = current.Revenue;
				model.Expenses = current.Expenses;
				model.NetProfit = current.Revenue - current.Expenses;
				model.NetProfitPrevious = previous.Revenue - previous.Expenses;
			}

			// =========================================
			// الخزينة والبنوك
			// =========================================

			if (model.CanTreasury)
			{
				var cashAccounts = await _context.CashAccounts
					.AsNoTracking()
					.Where(x => x.Name != "إيراد العمولات")
					.Select(x => new { x.Id, x.Name, x.IsActive })
					.ToListAsync();

				var cashIds = cashAccounts.Select(x => x.Id).ToList();

				var outgoing = await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(t => cashIds.Contains(t.CashAccountId))
					.GroupBy(t => new { t.CashAccountId, t.Type })
					.Select(g => new
					{
						g.Key.CashAccountId,
						g.Key.Type,
						Sum = g.Sum(t => t.Amount)
					})
					.ToListAsync();

				var incoming = await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(t => t.TransferToCashAccountId != null &&
								cashIds.Contains(t.TransferToCashAccountId.Value))
					.GroupBy(t => t.TransferToCashAccountId!.Value)
					.Select(g => new { Id = g.Key, Sum = g.Sum(t => t.Amount) })
					.ToDictionaryAsync(x => x.Id, x => x.Sum);

				var balances = new List<NamedAmount>();

				foreach (var acc in cashAccounts)
				{
					decimal balance = 0m;

					foreach (var o in outgoing.Where(x => x.CashAccountId == acc.Id))
					{
						balance += o.Type switch
						{
							TreasuryTransactionType.Receive => o.Sum,
							TreasuryTransactionType.Adjust => o.Sum,
							TreasuryTransactionType.Pay => -o.Sum,
							TreasuryTransactionType.Transfer => -o.Sum,
							TreasuryTransactionType.WalletTransfer => -o.Sum,
							_ => 0m
						};
					}

					if (incoming.TryGetValue(acc.Id, out var inSum))
						balance += inSum;

					model.CashBalance += balance;

					if (acc.IsActive)
					{
						balances.Add(new NamedAmount
						{
							Id = acc.Id,
							Name = acc.Name,
							Amount = balance
						});
					}
				}

				model.CashAccounts = balances
					.OrderByDescending(x => x.Amount)
					.Take(5)
					.ToList();

				model.TodayReceipts = await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(t => t.Type == TreasuryTransactionType.Receive && t.CreatedAt >= today)
					.SumAsync(t => (decimal?)t.Amount) ?? 0m;

				model.TodayPayments = await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(t => t.Type == TreasuryTransactionType.Pay && t.CreatedAt >= today)
					.SumAsync(t => (decimal?)t.Amount) ?? 0m;
			}

			// =========================================
			// أرصدة العملاء
			// =========================================

			if (model.CanCustomers)
			{
				var customers = await _context.Customers
					.AsNoTracking()
					.Select(c => new { c.Id, c.Name, c.OpeningBalance })
					.ToListAsync();

				var invoiced = await _context.SalesInvoices
					.AsNoTracking()
					.Where(x => x.Status == SalesInvoiceStatus.Confirmed && x.CustomerId != null)
					.GroupBy(x => x.CustomerId!.Value)
					.Select(g => new { Id = g.Key, Sum = g.Sum(x => x.TotalAmount) })
					.ToDictionaryAsync(x => x.Id, x => x.Sum);

				var returned = await _context.SalesReturnInvoices
					.AsNoTracking()
					.GroupBy(x => x.CustomerId)
					.Select(g => new { Id = g.Key, Sum = g.Sum(x => x.TotalAmount) })
					.ToDictionaryAsync(x => x.Id, x => x.Sum);

				var received = await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x => x.Type == TreasuryTransactionType.Receive && x.CustomerId != null)
					.GroupBy(x => x.CustomerId!.Value)
					.Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Amount) })
					.ToDictionaryAsync(x => x.Id, x => x.Sum);

				var debtors = new List<NamedAmount>();

				foreach (var c in customers)
				{
					invoiced.TryGetValue(c.Id, out var inv);
					returned.TryGetValue(c.Id, out var ret);
					received.TryGetValue(c.Id, out var rec);

					var balance = c.OpeningBalance + inv - ret - rec;

					if (balance > 0)
					{
						model.Receivables += balance;
						debtors.Add(new NamedAmount { Id = c.Id, Name = c.Name, Amount = balance });
					}
				}

				model.TopDebtors = debtors
					.OrderByDescending(x => x.Amount)
					.Take(5)
					.ToList();

				model.PendingPortalRequests += await _context.Customers
					.CountAsync(x => x.PortalRequested && !x.PortalApproved);
			}

			// =========================================
			// أرصدة الموردين
			// =========================================

			if (model.CanSuppliers)
			{
				var suppliers = await _context.Suppliers
					.AsNoTracking()
					.Select(s => new { s.Id, s.Name, s.OpeningBalance })
					.ToListAsync();

				var invoiced = await _context.PurchaseInvoices
					.AsNoTracking()
					.Where(x => x.Status == PurchaseInvoiceStatus.Posted)
					.GroupBy(x => x.SupplierId)
					.Select(g => new { Id = g.Key, Sum = g.Sum(x => x.TotalAmount) })
					.ToDictionaryAsync(x => x.Id, x => x.Sum);

				var returned = await _context.PurchaseReturnInvoices
					.AsNoTracking()
					.GroupBy(x => x.SupplierId)
					.Select(g => new { Id = g.Key, Sum = g.Sum(x => x.TotalAmount) })
					.ToDictionaryAsync(x => x.Id, x => x.Sum);

				var paid = await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x => x.Type == TreasuryTransactionType.Pay && x.SupplierId != null)
					.GroupBy(x => x.SupplierId!.Value)
					.Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Amount) })
					.ToDictionaryAsync(x => x.Id, x => x.Sum);

				var creditors = new List<NamedAmount>();

				foreach (var s in suppliers)
				{
					invoiced.TryGetValue(s.Id, out var inv);
					returned.TryGetValue(s.Id, out var ret);
					paid.TryGetValue(s.Id, out var pay);

					var balance = s.OpeningBalance + inv - ret - pay;

					if (balance > 0)
					{
						model.Payables += balance;
						creditors.Add(new NamedAmount { Id = s.Id, Name = s.Name, Amount = balance });
					}
				}

				model.TopCreditors = creditors
					.OrderByDescending(x => x.Amount)
					.Take(5)
					.ToList();

				model.PendingPortalRequests += await _context.Suppliers
					.CountAsync(x => x.PortalRequested && !x.PortalApproved);
			}

			// =========================================
			// المخزون
			// =========================================

			if (model.CanInventory)
			{
				model.StockValue = await _context.StockLots
					.AsNoTracking()
					.Where(x => x.IsActive)
					.SumAsync(x => (decimal?)(x.QuantityRemaining * x.UnitCost)) ?? 0m;

				model.OutOfStockProducts = await _context.Products
					.AsNoTracking()
					.CountAsync(p => p.IsActive &&
									 !p.StockLots.Any(l => l.IsActive && l.QuantityRemaining > 0));

				var lowStock = _context.Products
					.AsNoTracking()
					.Where(p => p.IsActive && p.MinQuantity > 0)
					.Select(p => new LowStockItem
					{
						Id = p.Id,
						Name = p.Name,
						Min = p.MinQuantity,
						Remaining = p.StockLots
							.Where(l => l.IsActive)
							.Sum(l => (decimal?)l.QuantityRemaining) ?? 0m
					})
					.Where(x => x.Remaining <= x.Min);

				model.LowStockProducts = await lowStock.CountAsync();

				model.LowStockList = await lowStock
					.OrderBy(x => x.Remaining)
					.Take(6)
					.ToListAsync();
			}

			return View(model);
		}

		// =========================================
		// أدوات داخلية
		// =========================================

		private static (string Key, string Label, DateTime From) ResolveRange(
			string? range,
			DateTime today)
		{
			return (range ?? "month").ToLowerInvariant() switch
			{
				"today" => ("today", "اليوم", today),
				"7d" => ("7d", "آخر ٧ أيام", today.AddDays(-6)),
				"3m" => ("3m", "آخر ٣ شهور",
					new DateTime(today.Year, today.Month, 1).AddMonths(-2)),
				"year" => ("year", "السنة الحالية", new DateTime(today.Year, 1, 1)),
				_ => ("month", "الشهر الحالي", new DateTime(today.Year, today.Month, 1))
			};
		}

		private static List<TrendPoint> BuildTrend(
			DateTime chartFrom,
			DateTime today,
			Dictionary<DateTime, decimal> sales,
			Dictionary<DateTime, decimal> purchases)
		{
			var culture = System.Globalization.CultureInfo.GetCultureInfo("ar-EG");
			var days = (today - chartFrom).Days + 1;

			DateTime KeyOf(DateTime d)
			{
				if (days <= 45) return d.Date;

				if (days <= 150)
				{
					var offset = ((int)d.DayOfWeek + 1) % 7;
					return d.Date.AddDays(-offset);
				}

				return new DateTime(d.Year, d.Month, 1);
			}

			string LabelOf(DateTime k) =>
				days > 150
					? k.ToString("MMM", culture)
					: k.ToString("dd/MM", culture);

			var buckets = new SortedDictionary<DateTime, TrendPoint>();

			for (var d = chartFrom.Date; d <= today; d = d.AddDays(1))
			{
				var key = KeyOf(d);

				if (!buckets.ContainsKey(key))
					buckets[key] = new TrendPoint { Label = LabelOf(key) };

				if (sales.TryGetValue(d, out var s))
					buckets[key].Sales += s;

				if (purchases.TryGetValue(d, out var p))
					buckets[key].Purchases += p;
			}

			return buckets.Values.ToList();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		[AllowAnonymous]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}

		[HttpGet]
		[AdminOnly]
		public async Task<IActionResult> FactoryResetData()
		{
			var model = new FactoryResetDataViewModel
			{
				Branches = await _context.Branches.CountAsync(),
				Stores = await _context.Stores.CountAsync(),
				CashAccounts = await _context.CashAccounts.CountAsync(),
				ChartAccounts = await _context.ChartAccounts.CountAsync(),
				Customers = await _context.Customers.CountAsync(),
				Suppliers = await _context.Suppliers.CountAsync(),
				Employees = await _context.Employees.CountAsync(),
				Users = await _context.Users.CountAsync(),
				Products = await _context.Products.CountAsync(),
				Categories = await _context.Categories.CountAsync(),
				Units = await _context.Units.CountAsync(),
				StockLots = await _context.StockLots.CountAsync(),
				SalesInvoices = await _context.SalesInvoices.CountAsync(),
				PurchaseInvoices = await _context.PurchaseInvoices.CountAsync(),
				TreasuryTransactions = await _context.TreasuryTransactions.CountAsync(),
				JournalEntries = await _context.JournalEntries.CountAsync(),
				StockTransactions = await _context.StockTransactions.CountAsync(),
				Companies = await _context.Companies.CountAsync()
			};

			return View(model);
		}

		public const string ResetConfirmationWord = "إعادة ضبط المصنع";

		[HttpPost]
		[ValidateAntiForgeryToken]
		[AdminOnly]
		public async Task<IActionResult> FactoryReset(FactoryResetViewModel model)
		{
			if (!ModelState.IsValid)
			{
				TempData["Error"] = "البيانات غير مكتملة.";
				return RedirectToAction(nameof(Index));
			}

			var currentUserId = int.TryParse(
				User.FindFirstValue(ClaimTypes.NameIdentifier),
				out var uid)
					? uid
					: 0;

			if (currentUserId == 0)
			{
				TempData["Error"] = "تعذر التعرف على حساب الأدمن.";
				return RedirectToAction(nameof(Index));
			}

			var adminUser = await _context.Users
				.FirstOrDefaultAsync(x =>
					x.Id == currentUserId &&
					x.UserType == UserType.Admin);

			if (adminUser == null || adminUser.PasswordHash == null)
			{
				TempData["Error"] = "حساب الأدمن غير موجود.";
				return RedirectToAction(nameof(Index));
			}

			var hasher = new PasswordHasher<User>();

			var verify = hasher.VerifyHashedPassword(
				adminUser,
				adminUser.PasswordHash,
				model.Password ?? string.Empty);

			if (verify != PasswordVerificationResult.Success)
			{
				TempData["Error"] = "كلمة المرور غير صحيحة.";
				return RedirectToAction(nameof(Index));
			}

			if (model.ConfirmationWord?.Trim() != ResetConfirmationWord)
			{
				TempData["Error"] = "كلمة التأكيد غير متطابقة.";
				return RedirectToAction(nameof(Index));
			}

			try
			{
				var database = "AccountingSystemDb";
				var backupPath = Path.Combine(
					_environment.ContentRootPath,
					"Backups",
					$"AccountingSystemDb_FactoryReset_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak");

				Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

				var backupSql =
					$"BACKUP DATABASE [{database}] TO DISK = N'{backupPath.Replace("'", "''")}' WITH INIT";

				await _context.Database.ExecuteSqlRawAsync(backupSql);
			}
			catch
			{
				// النسخة الاحتياطية اختيارية
			}

			await _context.AuditLogs.AddAsync(new AuditLog
			{
				UserId = currentUserId,
				UserName = User.Identity?.Name ?? "Unknown",
				Action = "FactoryReset",
				EntityName = "System",
				EntityId = "All",
				Summary = "تم ضبط النظام بالكامل",
				CreatedAt = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();

			var strategy = _context.Database.CreateExecutionStrategy();

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM SalesInvoiceItemLots;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM SalesInvoiceItems;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM StockTransactionItems;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM StockTransactions;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM StockLots;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM SalesInvoices;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM PurchaseInvoiceItems;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM PurchaseInvoices;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM SalesReturnInvoiceItems;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM SalesReturnInvoices;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM PurchaseReturnInvoiceItems;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM PurchaseReturnInvoices;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM JournalEntryLines;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM JournalEntries;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM TreasuryTransactions;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM CashAccounts;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM ChartAccounts;");

						await DbSeeder.SeedChartOfAccountsAsync(_context);

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM ProductCategories;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM ProductUnits;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Products;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Categories;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Units;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM EmployeeBranches;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM EmployeeStores;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM AppNotifications;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM AuditLogs;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM PasswordResetRequests;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM UserPermissions WHERE UserId <> @p0;",
							currentUserId);

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Users WHERE Id <> @p0;",
							currentUserId);

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Employees;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Customers;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Suppliers;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Stores;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Branches;");

						await _context.Database.ExecuteSqlRawAsync(
							"DELETE FROM Companies;");

						await transaction.CommitAsync();

						try
						{
							var configPath = Path.Combine(
								_environment.ContentRootPath,
								"appsettings.json");

							if (System.IO.File.Exists(configPath))
							{
								var json = System.IO.File.ReadAllText(configPath);
								var root = System.Text.Json.Nodes.JsonNode.Parse(json)
									as System.Text.Json.Nodes.JsonObject;

								var gmNode = root?["InitialGeneralManager"]
									as System.Text.Json.Nodes.JsonObject;

								if (gmNode != null)
								{
									gmNode["Enabled"] = false;

									System.IO.File.WriteAllText(
										configPath,
										root!.ToJsonString(
											new System.Text.Json.JsonSerializerOptions
											{
												WriteIndented = true
											}));
								}
							}
						}
						catch
						{
							// فشل تعديل الإعدادات لن يوقف الضبط
						}

						TempData["Success"] = "تمت إعادة ضبط المصنع بنجاح — حُذفت جميع بيانات العمل مع بقاء حساب الأدمن فقط.";
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();

						TempData["Error"] = "فشلت عملية إعادة الضبط: " + ex.Message;
					}
				});

			return RedirectToAction(nameof(Index));
		}
	}
}