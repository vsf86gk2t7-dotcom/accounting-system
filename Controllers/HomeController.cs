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
		private readonly ILogger<HomeController> _logger;
		private readonly IConfiguration _configuration;

		public HomeController(
			ApplicationDbContext context,
			IWebHostEnvironment environment,
			IPermissionService permissionService,
			ILogger<HomeController> logger,
			IConfiguration configuration)
		{
			_context = context;
			_environment = environment;
			_permissionService = permissionService;
			_logger = logger;
			_configuration = configuration;
		}

		[Authorize(Roles = "Admin,Employee")]
		public async Task<IActionResult> Index(string? range)
		{
			var today = DateTime.Today;
			var (rangeKey, rangeLabel, periodFrom) = ResolveRange(range, today);
			var to = today.AddDays(1);
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

			model.Companies = await _context.Companies.CountAsync();
			model.Branches = await _context.Branches.CountAsync();
			model.Stores = await _context.Stores.CountAsync();
			model.Products = await _context.Products.CountAsync();
			model.Categories = await _context.Categories.CountAsync();
			model.Units = await _context.Units.CountAsync();
			model.Customers = await _context.Customers.CountAsync();
			model.Suppliers = await _context.Suppliers.CountAsync();
			model.Employees = await _context.Employees.CountAsync();
			model.Users = await _context.Users.CountAsync();
			model.SalesInvoices = await _context.SalesInvoices.CountAsync();
			model.PurchaseInvoices = await _context.PurchaseInvoices.CountAsync();

			model.ConfirmedSales = await _context.SalesInvoices
				.CountAsync(x => x.Status == SalesInvoiceStatus.Confirmed);

			model.PostedPurchases = await _context.PurchaseInvoices
				.CountAsync(x => x.Status == PurchaseInvoiceStatus.Posted);

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

			model.Trend = BuildTrend(chartFrom, today, salesByDay, purchasesByDay);

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
					.Select(p => new DashboardItemViewModel
					{
						Id = p.Id,
						Name = p.Name,
						Min = (int)p.MinQuantity,
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

		// =========================================
		// إعادة ضبط المصنع — شاشة القراءة فقط
		// =========================================

		[HttpGet]
		[AdminOnly]
		public async Task<IActionResult> FactoryResetData()
		{
			var model = new FactoryResetDataViewModel
			{
				// أطراف التعامل
				Customers = await _context.Customers.CountAsync(),
				Suppliers = await _context.Suppliers.CountAsync(),
				Employees = await _context.Employees.CountAsync(),
				Users = await _context.Users.CountAsync(),

				// الهيكل التنظيمي
				Branches = await _context.Branches.CountAsync(),
				Stores = await _context.Stores.CountAsync(),
				CashAccounts = await _context.CashAccounts.CountAsync(),
				Departments = await _context.Departments.CountAsync(),
				Positions = await _context.Positions.CountAsync(),

				// الحسابات والقيود
				ChartAccounts = await _context.ChartAccounts.CountAsync(),
				JournalEntries = await _context.JournalEntries.CountAsync(),
				JournalEntryLines = await _context.JournalEntryLines.CountAsync(),

				// الأصناف والمخزون
				Products = await _context.Products.CountAsync(),
				Categories = await _context.Categories.CountAsync(),
				Units = await _context.Units.CountAsync(),
				StockLots = await _context.StockLots.CountAsync(),
				StockTransactions = await _context.StockTransactions.CountAsync(),
				StockTransactionItems = await _context.StockTransactionItems.CountAsync(),
				SupplierProductPackings = await _context.SupplierProductPackings.CountAsync(),

				// الفواتير والحركات
				SalesInvoices = await _context.SalesInvoices.CountAsync(),
				SalesInvoiceItems = await _context.SalesInvoiceItems.CountAsync(),
				SalesReturnInvoices = await _context.SalesReturnInvoices.CountAsync(),
				SalesReturnInvoiceItems = await _context.SalesReturnInvoiceItems.CountAsync(),
				PurchaseInvoices = await _context.PurchaseInvoices.CountAsync(),
				PurchaseInvoiceItems = await _context.PurchaseInvoiceItems.CountAsync(),
				PurchaseReturnInvoices = await _context.PurchaseReturnInvoices.CountAsync(),
				PurchaseReturnInvoiceItems = await _context.PurchaseReturnInvoiceItems.CountAsync(),
				TreasuryTransactions = await _context.TreasuryTransactions.CountAsync(),

				// المناديب والشحن
				SalesRepProfiles = await _context.SalesRepProfiles.CountAsync(),
				ShippingBills = await _context.ShippingBills.CountAsync(),
				ShippingCompanies = await _context.ShippingCompanies.CountAsync(),

				// الموارد البشرية
				Attendances = await _context.Attendances.CountAsync(),
				LeaveRequests = await _context.LeaveRequests.CountAsync(),
				EmployeeDeductions = await _context.EmployeeDeductions.CountAsync(),
				EmployeeAdvances = await _context.EmployeeAdvances.CountAsync(),
				PayrollRuns = await _context.PayrollRuns.CountAsync(),
				PayrollItems = await _context.PayrollItems.CountAsync(),

				// النظام
				Companies = await _context.Companies.CountAsync(),
				AuditLogs = await _context.AuditLogs.CountAsync(),
				AppNotifications = await _context.AppNotifications.CountAsync()
			};

			return View(model);
		}

		// =========================================
		// إعادة ضبط المصنع — التنفيذ الفعلي
		// =========================================

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

			// =========================================
			// النسخة الاحتياطية — إلزامية قبل أي حذف
			// المسار يُقرأ من BackupSettings:BackupFolder
			// =========================================
			string? backupPath = null;

			if (_context.Database.IsRelational())
			{
				try
				{
					var database = _context.Database.GetDbConnection().Database;

					// ✅ اقرأ المجلد من الإعدادات
					var backupFolder = _configuration["BackupSettings:BackupFolder"];

					if (string.IsNullOrWhiteSpace(backupFolder))
						backupFolder = "Backups";

					// إن كان نسبيًا → ادمجه مع ContentRootPath
					if (!Path.IsPathRooted(backupFolder))
					{
						backupFolder = Path.Combine(
							_environment.ContentRootPath,
							backupFolder);
					}

					backupPath = Path.Combine(
						backupFolder,
						$"{database}_FactoryReset_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak");

					Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

					var backupSql =
						$"BACKUP DATABASE [{database}] TO DISK = N'{backupPath.Replace("'", "''")}' WITH INIT";

					await _context.Database.ExecuteSqlRawAsync(backupSql);

					// تحقق فعلي من وجود الملف وحجمه
					if (!System.IO.File.Exists(backupPath))
						throw new InvalidOperationException(
							"تم تنفيذ أمر BACKUP لكن ملف النسخة الاحتياطية لم يُنشأ على القرص.");

					var fileInfo = new FileInfo(backupPath);

					if (fileInfo.Length == 0)
						throw new InvalidOperationException(
							"ملف النسخة الاحتياطية موجود لكن حجمه صفر — النسخة غير صالحة.");

					_logger.LogInformation(
						"Backup created successfully. Path={Path}, SizeBytes={Size}",
						backupPath, fileInfo.Length);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex,
						"فشل إنشاء النسخة الاحتياطية قبل إعادة ضبط المصنع. المسار: {BackupPath}",
						backupPath ?? "(غير معروف)");

					TempData["Error"] =
						$"❌ فشل إنشاء النسخة الاحتياطية: {ex.Message}. " +
						"لن يتم تنفيذ إعادة الضبط دون نسخة احتياطية ناجحة. " +
						"تأكد من صلاحيات الكتابة على مجلد النسخ الاحتياطي ومساحة القرص.";

					return RedirectToAction(nameof(FactoryResetData));
				}
			}

			// تسجيل الحركة (قبل الحذف)
			await _context.AuditLogs.AddAsync(new AuditLog
			{
				UserId = currentUserId,
				UserName = User.Identity?.Name ?? "Unknown",
				Action = "FactoryReset",
				EntityName = "System",
				EntityId = "All",
				Summary = $"تم ضبط النظام بالكامل. نسخة احتياطية: {backupPath ?? "InMemory"}",
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
						if (_context.Database.IsRelational())
						{
							// =====================================
							// المرحلة 1
							// =====================================
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM Attendances;
								DELETE FROM EmployeeDeductions;
								DELETE FROM EmployeeBranches;
								DELETE FROM EmployeeStores;
								DELETE FROM LeaveRequests;
								DELETE FROM SalesInvoiceItemLots;
								DELETE FROM SalesReturnItemLots;
								DELETE FROM ShippingBills;
								DELETE FROM StockTransactionItems;
								DELETE FROM ProductCategories;
								DELETE FROM ProductUnits;
								DELETE FROM AppNotifications;
								DELETE FROM PasswordResetRequests;
								DELETE FROM PaymentReminders;
								DELETE FROM PurchaseReturnInvoiceItems;
								DELETE FROM TreasuryTransactions;
								DELETE FROM SupplierProductPackings;
								DELETE FROM EmployeeAdvances;
								DELETE FROM JournalEntryLines;
							");

							await _context.Database.ExecuteSqlRawAsync(
								"DELETE FROM UserPermissions WHERE UserId <> @p0;",
								currentUserId);

							// ---------- المرحلة 2 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM PayrollItems;
								DELETE FROM SalesReturnInvoiceItems;
								DELETE FROM StockTransactions;
								DELETE FROM PurchaseReturnInvoices;
							");

							// ---------- المرحلة 3 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM PayrollRuns;
								DELETE FROM SalesInvoiceItems;
								DELETE FROM SalesReturnInvoices;
							");

							// ---------- المرحلة 4 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM StockLots;
								DELETE FROM PurchaseInvoiceItems;
								DELETE FROM SalesInvoices;
								DELETE FROM JournalEntries;
							");

							// ---------- المرحلة 5 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM Products;
								DELETE FROM PurchaseInvoices;
							");

							// ---------- المرحلة 6 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM Categories;
								DELETE FROM Units;
							");

							// ---------- المرحلة 7 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								UPDATE Users
								SET CustomerId = NULL,
									SupplierId = NULL,
									EmployeeId = NULL
								WHERE Id = @p0;

								DELETE FROM Users WHERE Id <> @p0;
							", currentUserId);

							// ---------- المرحلة 8 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM Customers;
								DELETE FROM Suppliers;
							");

							// ---------- المرحلة 9 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM SalesRepProfiles;
								DELETE FROM Employees;
							");

							// ---------- المرحلة 10 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM Departments;
								DELETE FROM Positions;
								DELETE FROM CashAccounts;
								DELETE FROM Stores;
							");

							// ---------- المرحلة 11 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								UPDATE ChartAccounts SET ParentId = NULL;
								DELETE FROM ChartAccounts;
							");

							await DbSeeder.SeedChartOfAccountsAsync(_context);

							// ---------- المرحلة 12 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM Branches;
								DELETE FROM ShippingCompanies;
							");

							// ---------- المرحلة 13 ----------
							await _context.Database.ExecuteSqlRawAsync(@"
								DELETE FROM Companies;
								DELETE FROM AuditLogs;
								DELETE FROM FiscalPeriods;
								DELETE FROM SequenceCounters;
							");
						}
						else
						{
							// =====================================
							// InMemory: نفس الترتيب عبر LINQ
							// =====================================

							_context.Attendances.RemoveRange(_context.Attendances.ToList());
							_context.EmployeeDeductions.RemoveRange(_context.EmployeeDeductions.ToList());
							_context.EmployeeBranches.RemoveRange(_context.EmployeeBranches.ToList());
							_context.EmployeeStores.RemoveRange(_context.EmployeeStores.ToList());
							_context.LeaveRequests.RemoveRange(_context.LeaveRequests.ToList());
							_context.SalesInvoiceItemLots.RemoveRange(_context.SalesInvoiceItemLots.ToList());
							_context.SalesReturnItemLots.RemoveRange(_context.SalesReturnItemLots.ToList());
							_context.ShippingBills.RemoveRange(_context.ShippingBills.ToList());
							_context.StockTransactionItems.RemoveRange(_context.StockTransactionItems.ToList());
							_context.ProductCategories.RemoveRange(_context.ProductCategories.ToList());
							_context.ProductUnits.RemoveRange(_context.ProductUnits.ToList());
							_context.AppNotifications.RemoveRange(_context.AppNotifications.ToList());
							_context.PasswordResetRequests.RemoveRange(_context.PasswordResetRequests.ToList());
							_context.PaymentReminders.RemoveRange(_context.PaymentReminders.ToList());
							_context.PurchaseReturnInvoiceItems.RemoveRange(_context.PurchaseReturnInvoiceItems.ToList());
							_context.TreasuryTransactions.RemoveRange(_context.TreasuryTransactions.ToList());
							_context.SupplierProductPackings.RemoveRange(_context.SupplierProductPackings.ToList());
							_context.EmployeeAdvances.RemoveRange(_context.EmployeeAdvances.ToList());
							_context.JournalEntryLines.RemoveRange(_context.JournalEntryLines.ToList());
							_context.UserPermissions.RemoveRange(
								_context.UserPermissions.Where(x => x.UserId != currentUserId).ToList());

							await _context.SaveChangesAsync();

							_context.PayrollItems.RemoveRange(_context.PayrollItems.ToList());
							_context.SalesReturnInvoiceItems.RemoveRange(_context.SalesReturnInvoiceItems.ToList());
							_context.StockTransactions.RemoveRange(_context.StockTransactions.ToList());
							_context.PurchaseReturnInvoices.RemoveRange(_context.PurchaseReturnInvoices.ToList());
							await _context.SaveChangesAsync();

							_context.PayrollRuns.RemoveRange(_context.PayrollRuns.ToList());
							_context.SalesInvoiceItems.RemoveRange(_context.SalesInvoiceItems.ToList());
							_context.SalesReturnInvoices.RemoveRange(_context.SalesReturnInvoices.ToList());
							await _context.SaveChangesAsync();

							_context.StockLots.RemoveRange(_context.StockLots.ToList());
							_context.PurchaseInvoiceItems.RemoveRange(_context.PurchaseInvoiceItems.ToList());
							_context.SalesInvoices.RemoveRange(_context.SalesInvoices.ToList());
							_context.JournalEntries.RemoveRange(_context.JournalEntries.ToList());
							await _context.SaveChangesAsync();

							_context.Products.RemoveRange(_context.Products.ToList());
							_context.PurchaseInvoices.RemoveRange(_context.PurchaseInvoices.ToList());
							await _context.SaveChangesAsync();

							_context.Categories.RemoveRange(_context.Categories.ToList());
							_context.Units.RemoveRange(_context.Units.ToList());
							await _context.SaveChangesAsync();

							foreach (var u in _context.Users.Where(x => x.Id == currentUserId).ToList())
							{
								u.CustomerId = null;
								u.SupplierId = null;
								u.EmployeeId = null;
							}
							_context.Users.RemoveRange(
								_context.Users.Where(x => x.Id != currentUserId).ToList());
							await _context.SaveChangesAsync();

							_context.Customers.RemoveRange(_context.Customers.ToList());
							_context.Suppliers.RemoveRange(_context.Suppliers.ToList());
							await _context.SaveChangesAsync();

							_context.SalesRepProfiles.RemoveRange(_context.SalesRepProfiles.ToList());
							_context.Employees.RemoveRange(_context.Employees.ToList());
							await _context.SaveChangesAsync();

							_context.Departments.RemoveRange(_context.Departments.ToList());
							_context.Positions.RemoveRange(_context.Positions.ToList());
							_context.CashAccounts.RemoveRange(_context.CashAccounts.ToList());
							_context.Stores.RemoveRange(_context.Stores.ToList());
							await _context.SaveChangesAsync();

							foreach (var ca in _context.ChartAccounts.ToList())
								ca.ParentId = null;
							await _context.SaveChangesAsync();
							_context.ChartAccounts.RemoveRange(_context.ChartAccounts.ToList());
							await _context.SaveChangesAsync();

							await DbSeeder.SeedChartOfAccountsAsync(_context);

							_context.Branches.RemoveRange(_context.Branches.ToList());
							_context.ShippingCompanies.RemoveRange(_context.ShippingCompanies.ToList());
							await _context.SaveChangesAsync();

							_context.Companies.RemoveRange(_context.Companies.ToList());
							_context.AuditLogs.RemoveRange(_context.AuditLogs.ToList());
							_context.FiscalPeriods.RemoveRange(_context.FiscalPeriods.ToList());
							_context.SequenceCounters.RemoveRange(_context.SequenceCounters.ToList());
							await _context.SaveChangesAsync();
						}

						await transaction.CommitAsync();

						// =====================================
						// إعادة إنشاء الفترة المحاسبية
						// =====================================
						try
						{
							await SeedFiscalPeriodAsync(_context);
						}
						catch (Exception ex)
						{
							_logger.LogWarning(ex,
								"فشل إنشاء الفترة المحاسبية بعد إعادة الضبط.");
						}

						// =====================================
						// تعطيل GeneralManager في appsettings.json
						// =====================================
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

						TempData["Success"] =
							"✅ تمت إعادة ضبط المصنع بنجاح — حُذفت جميع بيانات العمل مع بقاء حساب الأدمن فقط. " +
							$"نسخة احتياطية: {backupPath}";
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();

						_logger.LogError(ex,
							"فشل تنفيذ إعادة ضبط المصنع. النسخة الاحتياطية متاحة في: {BackupPath}",
							backupPath);

						TempData["Error"] =
							"فشلت عملية إعادة الضبط: " + ex.Message +
							$" — يمكنك استعادة البيانات من النسخة الاحتياطية: {backupPath}";
					}
				});

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// إنشاء فترة محاسبية افتراضية للسنة الحالية
		// =========================================
		private static async Task SeedFiscalPeriodAsync(ApplicationDbContext context)
		{
			var year = DateTime.UtcNow.Year;

			var exists = await context.FiscalPeriods
				.AnyAsync(p => p.StartDate.Year == year && !p.IsClosed);

			if (exists)
				return;

			context.FiscalPeriods.Add(new FiscalPeriod
			{
				PeriodName = $"السنة المالية {year}",
				StartDate = new DateTime(year, 1, 1),
				EndDate = new DateTime(year, 12, 31, 23, 59, 59),
				IsClosed = false,
				CreatedAt = DateTime.UtcNow
			});

			await context.SaveChangesAsync();
		}
	}
}