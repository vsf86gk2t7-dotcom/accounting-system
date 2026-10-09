using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	[Authorize]
	public class ReportsController : Controller
	{
		private readonly ApplicationDbContext _context;

		public ReportsController(
			ApplicationDbContext context)
		{
			_context = context;
		}

		// =========================================
		// صفحة التقارير الرئيسية
		// =========================================

		[HttpGet]
		public IActionResult Index()
		{
			return View();
		}

		// =========================================
		// تقرير المبيعات
		// =========================================

		[HttpGet]
		[RequirePermission("report.sales")]
		public async Task<IActionResult> SalesReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new SalesReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var invoices =
				await _context.SalesInvoices
					.AsNoTracking()
					.Include(x => x.Customer)
					.Include(x => x.Store)
					.Where(x =>
						x.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= from &&
						x.InvoiceDate < to)
					.OrderBy(x => x.InvoiceDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			foreach (var invoice in invoices)
			{
				model.Rows.Add(
					new SalesReportRowViewModel
					{
						InvoiceNumber =
							invoice.InvoiceNumber,
						InvoiceDate = invoice.InvoiceDate,
						CustomerName =
							invoice.Customer?.Name ?? "نقدي",
						StoreName =
							invoice.Store?.Name ?? "-",
						PaymentMethod =
							invoice.PaymentMethod,
						PaymentDetails = invoice.PaymentDetails,
						TotalAmount = invoice.TotalAmount
					});

				model.GrandTotal += invoice.TotalAmount;
			}

			model.InvoiceCount = invoices.Count;

			return View(model);
		}

		// =========================================
		// تقرير المشتريات
		// =========================================

		[HttpGet]
		[RequirePermission("report.purchase")]
		public async Task<IActionResult> PurchaseReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new PurchaseReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var invoices =
				await _context.PurchaseInvoices
					.AsNoTracking()
					.Include(x => x.Supplier)
					.Include(x => x.Store)
					.Where(x =>
						x.Status ==
							PurchaseInvoiceStatus.Posted &&
						x.InvoiceDate >= from &&
						x.InvoiceDate < to)
					.OrderBy(x => x.InvoiceDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			foreach (var invoice in invoices)
			{
				model.Rows.Add(
					new PurchaseReportRowViewModel
					{
						InvoiceNumber =
							invoice.InvoiceNumber,
						InvoiceDate = invoice.InvoiceDate,
						SupplierName =
							invoice.Supplier?.Name ?? "-",
						StoreName =
							invoice.Store?.Name ?? "-",
						TotalAmount = invoice.TotalAmount
					});

				model.GrandTotal += invoice.TotalAmount;
			}

			model.InvoiceCount = invoices.Count;

			return View(model);
		}

		// =========================================
		// تقرير المخزون
		// =========================================

		[HttpGet]
		[RequirePermission("report.inventory")]
		public async Task<IActionResult> InventoryReport(
			int? storeId)
		{
			var model =
				new InventoryReportViewModel
				{
					StoreId = storeId
				};

			model.Stores =
				await _context.Stores
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();

			var lotsQuery =
				_context.StockLots
					.AsNoTracking()
					.Include(x => x.Product)
					.Include(x => x.Store)
					.Where(x => x.IsActive);

			if (storeId.HasValue)
			{
				lotsQuery =
					lotsQuery.Where(x =>
						x.StoreId == storeId.Value);
			}

			var lots =
				await lotsQuery.ToListAsync();

			var groups =
				lots
					.Where(x =>
						x.Product != null &&
						x.Store != null)
					.GroupBy(x =>
						new
						{
							x.ProductId,
							x.StoreId
						});

			foreach (var group in groups)
			{
				var product =
					group.First().Product!;

				var store =
					group.First().Store!;

				var quantity =
					group.Sum(x =>
						x.QuantityRemaining);

				if (quantity <= 0)
				{
					continue;
				}

				var value =
					group.Sum(x =>
						x.QuantityRemaining * x.UnitCost);

				model.Rows.Add(
					new InventoryReportRowViewModel
					{
						ProductId = product.Id,
						ProductName = product.Name,
						ProductCode = product.Code,
						StoreName = store.Name,
						Quantity = quantity,
						AverageCost =
							value / quantity,
						Value = value
					});

				model.GrandValue += value;
			}

			model.Rows =
				model.Rows
					.OrderBy(x => x.ProductName)
					.ToList();

			return View(model);
		}

		// =========================================
		// تقرير الخزينة
		// =========================================

		[HttpGet]
		[RequirePermission("report.treasury")]
		public async Task<IActionResult> TreasuryReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new TreasuryReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var transactions =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Include(x => x.CashAccount)
					.Include(x => x.Customer)
					.Include(x => x.Supplier)
					.Include(x => x.Employee)
					.Where(x =>
						x.CreatedAt >= from &&
						x.CreatedAt < to)
					.OrderBy(x => x.CreatedAt)
					.ThenBy(x => x.Id)
					.ToListAsync();

			foreach (var tx in transactions)
			{
				var isIn =
					tx.Type == TreasuryTransactionType.Receive ||
					(tx.Type == TreasuryTransactionType.Adjust &&
						tx.Amount > 0);

				var isOut =
					tx.Type == TreasuryTransactionType.Pay ||
					(tx.Type == TreasuryTransactionType.Adjust &&
						tx.Amount < 0);

				if (tx.Type == TreasuryTransactionType.Transfer)
				{
					continue;
				}

				var partyName =
					tx.Customer?.Name
					?? tx.Supplier?.Name
					?? tx.Employee?.Name;

				model.Rows.Add(
					new TreasuryReportRowViewModel
					{
						TransactionNumber =
							tx.TransactionNumber,
						CreatedAt = tx.CreatedAt,
						AccountName =
							tx.CashAccount?.Name ?? "-",
						Type = tx.Type,
						Side = isIn ? "قبض" : "صرف",
						Amount =
							isIn
								? tx.Amount
								: Math.Abs(tx.Amount),
						PartyName = partyName,
						Reason = tx.Reason
					});

				if (isIn)
				{
					model.GrandIn += tx.Amount;
				}
				else
				{
					model.GrandOut += Math.Abs(tx.Amount);
				}
			}

			return View(model);
		}

		// =========================================
		// تقرير القيود المحاسبية
		// =========================================

		[HttpGet]
		[RequirePermission("report.accounting")]
		public async Task<IActionResult> AccountingReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new AccountingReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var entries =
				await _context.JournalEntries
					.AsNoTracking()
					.Include(x => x.Lines)
					.Where(x =>
						x.EntryDate >= from &&
						x.EntryDate < to)
					.OrderBy(x => x.EntryDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			foreach (var entry in entries)
			{
				model.Rows.Add(
					new AccountingReportRowViewModel
					{
						EntryNumber = entry.EntryNumber,
						EntryDate = entry.EntryDate,
						Description = entry.Description,
						Debit =
							entry.Lines.Sum(x => x.Debit),
						Credit =
							entry.Lines.Sum(x => x.Credit),
						IsPosted = entry.IsPosted
					});

				model.GrandDebit +=
					entry.Lines.Sum(x => x.Debit);

				model.GrandCredit +=
					entry.Lines.Sum(x => x.Credit);
			}

			return View(model);
		}

		// =========================================
		// تقرير أرصدة العملاء (كل العملاء في صفحة واحدة)
		// =========================================

		[HttpGet]
		[RequirePermission("report.customer.statement")]
		public async Task<IActionResult> CustomerBalancesReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new CustomerBalancesReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var customers =
				await _context.Customers
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x => new
					{
						x.Id,
						x.Name,
						x.OpeningBalance
					})
					.ToListAsync();

			var invoicesBefore =
				await _context.SalesInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate < from)
					.GroupBy(x => x.CustomerId)
					.Select(g => new
					{
						CustomerId = g.Key,
						Total = g.Sum(x => x.TotalAmount)
					})
					.ToDictionaryAsync(
						x => x.CustomerId,
						x => x.Total);

			var returnsBefore =
				await _context.SalesReturnInvoices
					.AsNoTracking()
					.Where(x => x.ReturnDate < from)
					.GroupBy(x => x.CustomerId)
					.Select(g => new
					{
						CustomerId = g.Key,
						Total = g.Sum(x => x.TotalAmount)
					})
					.ToDictionaryAsync(
						x => x.CustomerId,
						x => x.Total);

			var paymentsBeforeEntities =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x =>
						x.Type == TreasuryTransactionType.Receive &&
						x.CustomerId != null &&
						x.CreatedAt < from)
					.ToListAsync();

			var paymentsBefore =
				paymentsBeforeEntities
					.GroupBy(x => x.CustomerId!.Value)
					.ToDictionary(
						x => x.Key,
						x => x.Sum(y => EffectiveCreditAmount(y)));

			var invoicesInPeriod =
				await _context.SalesInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= from &&
						x.InvoiceDate < to)
					.GroupBy(x => x.CustomerId)
					.Select(g => new
					{
						CustomerId = g.Key,
						Total = g.Sum(x => x.TotalAmount)
					})
					.ToDictionaryAsync(
						x => x.CustomerId,
						x => x.Total);

			var returnsInPeriod =
				await _context.SalesReturnInvoices
					.AsNoTracking()
					.Where(x =>
						x.ReturnDate >= from &&
						x.ReturnDate < to)
					.GroupBy(x => x.CustomerId)
					.Select(g => new
					{
						CustomerId = g.Key,
						Total = g.Sum(x => x.TotalAmount)
					})
					.ToDictionaryAsync(
						x => x.CustomerId,
						x => x.Total);

			var paymentsInPeriodEntities =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x =>
						x.Type == TreasuryTransactionType.Receive &&
						x.CustomerId != null &&
						x.CreatedAt >= from &&
						x.CreatedAt < to)
					.ToListAsync();

			var paymentsInPeriod =
				paymentsInPeriodEntities
					.GroupBy(x => x.CustomerId!.Value)
					.ToDictionary(
						x => x.Key,
						x => x.Sum(y => EffectiveCreditAmount(y)));

			foreach (var customer in customers)
			{
				var opening =
					customer.OpeningBalance;

				opening +=
					invoicesBefore
						.TryGetValue(
							customer.Id,
							out var beforeInv)
						? beforeInv
						: 0m;

				opening -=
					returnsBefore
						.TryGetValue(
							customer.Id,
							out var beforeRet)
						? beforeRet
						: 0m;

				opening -=
					paymentsBefore
						.TryGetValue(
							customer.Id,
							out var beforePay)
						? beforePay
						: 0m;

				var debit =
					invoicesInPeriod
						.TryGetValue(
							customer.Id,
							out var periodInv)
						? periodInv
						: 0m;

				var credit =
					returnsInPeriod
						.TryGetValue(
							customer.Id,
							out var periodRet)
						? periodRet
						: 0m;

				credit +=
					paymentsInPeriod
						.TryGetValue(
							customer.Id,
							out var periodPay)
						? periodPay
						: 0m;

				var balance =
					opening + debit - credit;

				model.Customers.Add(
					new CustomerBalanceRowViewModel
					{
						CustomerId = customer.Id,
						CustomerName = customer.Name,
						OpeningBalance = opening,
						Debit = debit,
						Credit = credit,
						Balance = balance
					});

				model.GrandOpening += opening;
				model.GrandDebit += debit;
				model.GrandCredit += credit;
				model.GrandBalance += balance;
			}

			return View(model);
		}

		// =========================================
		// كشف حساب عميل
		// =========================================

		[HttpGet]
		[RequirePermission("report.customer.statement")]
		public async Task<IActionResult> CustomerStatement(
			DateTime? fromDate,
			DateTime? toDate,
			int? customerId)
		{
			var model =
				new CustomerStatementViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today,
					CustomerId = customerId ?? 0
				};

			await LoadCustomersAsync(model);

			if (model.CustomerId == 0)
			{
				return View(model);
			}

			var customer =
				await _context.Customers
					.AsNoTracking()
					.FirstOrDefaultAsync(x =>
						x.Id == model.CustomerId);

			if (customer == null)
			{
				ModelState.AddModelError(
					nameof(model.CustomerId),
					"العميل غير موجود.");

				return View(model);
			}

			model.CustomerName = customer.Name;

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var balance =
				customer.OpeningBalance;

			model.OpeningBalance = balance;

			var invoices =
				await _context.SalesInvoices
					.AsNoTracking()
					.Where(x =>
						x.CustomerId == model.CustomerId &&
						x.Status ==
							SalesInvoiceStatus.Confirmed)
					.OrderBy(x => x.InvoiceDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			var returns =
				await _context.SalesReturnInvoices
					.AsNoTracking()
					.Where(x =>
						x.CustomerId == model.CustomerId)
					.OrderBy(x => x.ReturnDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			var payments =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x =>
						x.CustomerId == model.CustomerId &&
						x.Type ==
							TreasuryTransactionType.Receive)
					.OrderBy(x => x.CreatedAt)
					.ThenBy(x => x.Id)
					.ToListAsync();

			foreach (var invoice in invoices)
			{
				if (invoice.InvoiceDate >= from)
				{
					continue;
				}

				balance += invoice.TotalAmount;
			}

			foreach (var item in returns)
			{
				if (item.ReturnDate >= from)
				{
					continue;
				}

				balance -= item.TotalAmount;
			}

			foreach (var payment in payments)
			{
				if (payment.CreatedAt >= from)
				{
					continue;
				}

				balance -= EffectiveCreditAmount(payment);
			}

			model.OpeningBalance = balance;

			foreach (var invoice in invoices)
			{
				if (invoice.InvoiceDate < from ||
					invoice.InvoiceDate >= to)
				{
					continue;
				}

				balance += invoice.TotalAmount;

				model.Rows.Add(
					new StatementRowViewModel
					{
						Date = invoice.InvoiceDate,
						Description =
							$"فاتورة بيع {invoice.InvoiceNumber}",
						Debit = invoice.TotalAmount,
						Balance = balance,

						// ✅ جديد
						SourceType = "SalesInvoice",
						SourceId = invoice.Id,
						ReferenceNumber = invoice.InvoiceNumber,
						DetailsUrl = Url.Action(
							"Details", "SalesInvoice",
							new { id = invoice.Id })
					});
			}

			foreach (var item in returns)
			{
				if (item.ReturnDate < from ||
					item.ReturnDate >= to)
				{
					continue;
				}

				balance -= item.TotalAmount;

				model.Rows.Add(
					new StatementRowViewModel
					{
						Date = item.ReturnDate,
						Description =
							$"مرتجع بيع {item.InvoiceNumber}",
						Credit = item.TotalAmount,
						Balance = balance,

						// ✅ جديد
						SourceType = "SalesReturn",
						SourceId = item.Id,
						ReferenceNumber = item.InvoiceNumber,
						DetailsUrl = Url.Action(
							"Details", "SalesReturnInvoice",
							new { id = item.Id })
					});
			}

			foreach (var payment in payments)
			{
				if (payment.CreatedAt < from ||
					payment.CreatedAt >= to)
				{
					continue;
				}

				var effectiveCredit =
					EffectiveCreditAmount(payment);

				balance -= effectiveCredit;

				model.Rows.Add(
					new StatementRowViewModel
					{
						Date = payment.CreatedAt,
						Description =
							$"سداد {payment.TransactionNumber}",
						Credit = effectiveCredit,
						Balance = balance,

						// ✅ جديد
						SourceType = "CustomerPayment",
						SourceId = payment.Id,
						ReferenceNumber = payment.TransactionNumber,
						DetailsUrl = Url.Action(
							"Details", "TreasuryTransaction",
							new { id = payment.Id })
					});
			}

			model.Rows =
				model.Rows
					.OrderBy(x => x.Date)
					.ToList();

			model.ClosingBalance = balance;

			return View(model);
		}

		// =========================================
		// تقرير أرصدة الموردين (كل الموردين في صفحة واحدة)
		// =========================================

		[HttpGet]
		[RequirePermission("report.supplier.statement")]
		public async Task<IActionResult> SupplierBalancesReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new SupplierBalancesReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var suppliers =
				await _context.Suppliers
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x => new
					{
						x.Id,
						x.Name,
						x.OpeningBalance
					})
					.ToListAsync();

			var purchasesBefore =
				await _context.PurchaseInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status ==
							PurchaseInvoiceStatus.Posted &&
						x.InvoiceDate < from)
					.GroupBy(x => x.SupplierId)
					.Select(g => new
					{
						SupplierId = g.Key,
						Total = g.Sum(x => x.TotalAmount)
					})
					.ToDictionaryAsync(
						x => x.SupplierId,
						x => x.Total);

			var returnsBefore =
				await _context.PurchaseReturnInvoices
					.AsNoTracking()
					.Where(x => x.ReturnDate < from)
					.GroupBy(x => x.SupplierId)
					.Select(g => new
					{
						SupplierId = g.Key,
						Total = g.Sum(x => x.TotalAmount)
					})
					.ToDictionaryAsync(
						x => x.SupplierId,
						x => x.Total);

			var paymentsBeforeEntities =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x =>
						x.Type == TreasuryTransactionType.Pay &&
						x.SupplierId != null &&
						x.CreatedAt < from)
					.ToListAsync();

			var paymentsBefore =
				paymentsBeforeEntities
					.GroupBy(x => x.SupplierId!.Value)
					.ToDictionary(
						x => x.Key,
						x => x.Sum(y => EffectiveCreditAmount(y)));

			var purchasesInPeriod =
				await _context.PurchaseInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status ==
							PurchaseInvoiceStatus.Posted &&
						x.InvoiceDate >= from &&
						x.InvoiceDate < to)
					.GroupBy(x => x.SupplierId)
					.Select(g => new
					{
						SupplierId = g.Key,
						Total = g.Sum(x => x.TotalAmount)
					})
					.ToDictionaryAsync(
						x => x.SupplierId,
						x => x.Total);

			var returnsInPeriod =
				await _context.PurchaseReturnInvoices
					.AsNoTracking()
					.Where(x =>
						x.ReturnDate >= from &&
						x.ReturnDate < to)
					.GroupBy(x => x.SupplierId)
					.Select(g => new
					{
						SupplierId = g.Key,
						Total = g.Sum(x => x.TotalAmount)
					})
					.ToDictionaryAsync(
						x => x.SupplierId,
						x => x.Total);

			var paymentsInPeriodEntities =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x =>
						x.Type == TreasuryTransactionType.Pay &&
						x.SupplierId != null &&
						x.CreatedAt >= from &&
						x.CreatedAt < to)
					.ToListAsync();

			var paymentsInPeriod =
				paymentsInPeriodEntities
					.GroupBy(x => x.SupplierId!.Value)
					.ToDictionary(
						x => x.Key,
						x => x.Sum(y => EffectiveCreditAmount(y)));

			foreach (var supplier in suppliers)
			{
				var opening =
					supplier.OpeningBalance;

				opening +=
					purchasesBefore
						.TryGetValue(
							supplier.Id,
							out var beforePurch)
						? beforePurch
						: 0m;

				opening -=
					returnsBefore
						.TryGetValue(
							supplier.Id,
							out var beforeRet)
						? beforeRet
						: 0m;

				opening -=
					paymentsBefore
						.TryGetValue(
							supplier.Id,
							out var beforePay)
						? beforePay
						: 0m;

				var credit =
					purchasesInPeriod
						.TryGetValue(
							supplier.Id,
							out var periodPurch)
						? periodPurch
						: 0m;

				var debit =
					returnsInPeriod
						.TryGetValue(
							supplier.Id,
							out var periodRet)
						? periodRet
						: 0m;

				debit +=
					paymentsInPeriod
						.TryGetValue(
							supplier.Id,
							out var periodPay)
						? periodPay
						: 0m;

				var balance =
					opening + credit - debit;

				model.Suppliers.Add(
					new SupplierBalanceRowViewModel
					{
						SupplierId = supplier.Id,
						SupplierName = supplier.Name,
						OpeningBalance = opening,
						Debit = debit,
						Credit = credit,
						Balance = balance
					});

				model.GrandOpening += opening;
				model.GrandDebit += debit;
				model.GrandCredit += credit;
				model.GrandBalance += balance;
			}

			return View(model);
		}

		// =========================================
		// كشف حساب مورد
		// =========================================

		[HttpGet]
		[RequirePermission("report.supplier.statement")]
		public async Task<IActionResult> SupplierStatement(
			DateTime? fromDate,
			DateTime? toDate,
			int? supplierId)
		{
			var model =
				new SupplierStatementViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today,
					SupplierId = supplierId ?? 0
				};

			await LoadSuppliersAsync(model);

			if (model.SupplierId == 0)
			{
				return View(model);
			}

			var supplier =
				await _context.Suppliers
					.AsNoTracking()
					.FirstOrDefaultAsync(x =>
						x.Id == model.SupplierId);

			if (supplier == null)
			{
				ModelState.AddModelError(
					nameof(model.SupplierId),
					"المورد غير موجود.");

				return View(model);
			}

			model.SupplierName = supplier.Name;

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var balance =
				supplier.OpeningBalance;

			var invoices =
				await _context.PurchaseInvoices
					.AsNoTracking()
					.Where(x =>
						x.SupplierId == model.SupplierId &&
						x.Status ==
							PurchaseInvoiceStatus.Posted)
					.OrderBy(x => x.InvoiceDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			var returns =
				await _context.PurchaseReturnInvoices
					.AsNoTracking()
					.Where(x =>
						x.SupplierId == model.SupplierId)
					.OrderBy(x => x.ReturnDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			var payments =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x =>
						x.SupplierId == model.SupplierId &&
						x.Type == TreasuryTransactionType.Pay)
					.OrderBy(x => x.CreatedAt)
					.ThenBy(x => x.Id)
					.ToListAsync();

			foreach (var invoice in invoices)
			{
				if (invoice.InvoiceDate >= from)
				{
					continue;
				}

				balance += invoice.TotalAmount;
			}

			foreach (var item in returns)
			{
				if (item.ReturnDate >= from)
				{
					continue;
				}

				balance -= item.TotalAmount;
			}

			foreach (var payment in payments)
			{
				if (payment.CreatedAt >= from)
				{
					continue;
				}

				balance -= EffectiveCreditAmount(payment);
			}

			model.OpeningBalance = balance;

			foreach (var invoice in invoices)
			{
				if (invoice.InvoiceDate < from ||
					invoice.InvoiceDate >= to)
				{
					continue;
				}

				balance += invoice.TotalAmount;

				model.Rows.Add(
					new StatementRowViewModel
					{
						Date = invoice.InvoiceDate,
						Description =
							$"فاتورة شراء {invoice.InvoiceNumber}",
						Credit = invoice.TotalAmount,
						Balance = balance,

						// ✅ جديد: لفتح التفاصيل عند الدبل كليك
						SourceType = "PurchaseInvoice",
						SourceId = invoice.Id,
						ReferenceNumber = invoice.InvoiceNumber,
						DetailsUrl = Url.Action(
							"Details", "PurchaseInvoice",
							new { id = invoice.Id })
					});
			}

			foreach (var item in returns)
			{
				if (item.ReturnDate < from ||
					item.ReturnDate >= to)
				{
					continue;
				}

				balance -= item.TotalAmount;

				model.Rows.Add(
					new StatementRowViewModel
					{
						Date = item.ReturnDate,
						Description =
							$"مرتجع شراء {item.InvoiceNumber}",
						Debit = item.TotalAmount,
						Balance = balance,

						// ✅ جديد: لفتح التفاصيل عند الدبل كليك
						SourceType = "PurchaseReturn",
						SourceId = item.Id,
						ReferenceNumber = item.InvoiceNumber,
						DetailsUrl = Url.Action(
							"Details", "PurchaseReturnInvoice",
							new { id = item.Id })
					});
			}

			foreach (var payment in payments)
			{
				if (payment.CreatedAt < from ||
					payment.CreatedAt >= to)
				{
					continue;
				}

				var effectiveCredit =
					EffectiveCreditAmount(payment);

				balance -= effectiveCredit;

				model.Rows.Add(
					new StatementRowViewModel
					{
						Date = payment.CreatedAt,
						Description =
							$"سداد {payment.TransactionNumber}",
						Debit = effectiveCredit,
						Balance = balance,

						// ✅ جديد: لفتح التفاصيل عند الدبل كليك
						SourceType = "TreasuryPayment",
						SourceId = payment.Id,
						ReferenceNumber = payment.TransactionNumber,
						DetailsUrl = Url.Action(
							"Details", "TreasuryTransaction",
							new { id = payment.Id })
					});
			}

			model.Rows =
				model.Rows
					.OrderBy(x => x.Date)
					.ToList();

			model.ClosingBalance = balance;

			return View(model);
		}

		// =========================================
		// تقرير الأرباح
		// =========================================

		[HttpGet]
		[RequirePermission("report.sales")]
		public async Task<IActionResult> ProfitReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new ProfitReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

model.SalesBeforeTax =
				await _context.SalesInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= from &&
						x.InvoiceDate < to)
					.SumAsync(x => (decimal?)(x.TotalAmount - x.TaxAmount - x.SalesTaxAmount)) ?? 0m;

// الخصومات
model.Discounts =
				await _context.SalesInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= from &&
						x.InvoiceDate < to)
					.SumAsync(x => (decimal?)x.DiscountAmount) ?? 0m;

// صافي المبيعات بعد الخصومات وقبل الضرائب
// المبيعات قبل الضرائب (714.29) ناقص الخصم (5.71) = 708.58? 
// Wait - need to re-read the logic. 
// Actually the Arabic order says: "صافي المبيعات بعد الخصومات_before الضرائب"
// هذا يعني: المبيعات قبل الضرائب ناقص الخصم = 714.29 - 5.71 = 708.58
// ولكن looking at the user's expected output, they want:
// - "إجمالي المبيعات قبل الضرائب" = 714.29 (السعر بعد الخصم، قبل الضريبة)
// - "صافي المبيعات_after الخصومات_before الضرائب" = نفس 714.29 (لأن الخصم تم تطبيقه على السعر قبل الضريبة)
model.NetSales = model.SalesBeforeTax;

// تكلفة البضاعة المباعة
model.CostOfGoodsSold =

			model.CostOfGoodsSold =
				await _context.SalesInvoiceItemLots
					.AsNoTracking()
					.Where(x =>
						x.SalesInvoiceItem!.SalesInvoice!.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.SalesInvoiceItem.SalesInvoice.InvoiceDate >= from &&
						x.SalesInvoiceItem.SalesInvoice.InvoiceDate < to)
					.SumAsync(x => (decimal?)x.TotalCost) ?? 0m;

			var returnCost =
				await _context.SalesReturnItemLots
					.AsNoTracking()
					.Where(x =>
						x.SalesReturnInvoiceItem != null &&
						x.SalesReturnInvoiceItem.SalesReturnInvoice != null &&
						x.SalesReturnInvoiceItem.SalesReturnInvoice.ReturnDate >= from &&
						x.SalesReturnInvoiceItem.SalesReturnInvoice.ReturnDate < to)
					.SumAsync(x => (decimal?)x.TotalCost) ?? 0m;

			model.CostOfGoodsSold -= returnCost;

			model.GrossProfit =
				model.NetSales - model.CostOfGoodsSold;

			model.GrossMarginPercent =
				model.NetSales == 0
					? 0
					: Math.Round(
						model.GrossProfit /
						model.NetSales * 100m,
						2);

			model.Expenses =
				await _context.JournalEntryLines
					.AsNoTracking()
					.Where(x =>
						x.JournalEntry!.IsPosted &&
						x.JournalEntry.EntryDate >= from &&
						x.JournalEntry.EntryDate < to &&
						x.ChartAccount!.Type ==
							AccountType.Expense &&
						x.ChartAccount.Code != "5001")
					.SumAsync(x => x.Debit - x.Credit);

			model.NetProfit =
				model.GrossProfit - model.Expenses;

			return View(model);
		}

		// =========================================
		// تقرير الربح لكل صنف
		// =========================================

		[HttpGet]
		[RequirePermission("report.sales")]
		public async Task<IActionResult> ProductProfitReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new ProductProfitReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var items =
				await _context.SalesInvoiceItemLots
					.AsNoTracking()
					.Include(x => x.SalesInvoiceItem)
					.ThenInclude(x => x!.Product)
					.Where(x =>
						x.SalesInvoiceItem!.SalesInvoice!.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.SalesInvoiceItem.SalesInvoice.InvoiceDate >= from &&
						x.SalesInvoiceItem.SalesInvoice.InvoiceDate < to)
					.Select(x => new
					{
						ProductId = x.SalesInvoiceItem!.ProductId,
						ProductName = x.SalesInvoiceItem.Product!.Name,
						ProductCode = x.SalesInvoiceItem.Product.Code,
						QuantityBaseUnit = x.QuantityBaseUnit,
						UnitCost = x.UnitCost,
						TotalCost = x.TotalCost
					})
					.ToListAsync();

			var revenueItems =
				await _context.SalesInvoiceItems
					.AsNoTracking()
					.Include(x => x.Product)
					.Where(x =>
						x.SalesInvoice!.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.SalesInvoice.InvoiceDate >= from &&
						x.SalesInvoice.InvoiceDate < to)
					.Select(x => new
					{
						ProductId = x.ProductId,
						Revenue = x.TotalPrice
					})
					.ToListAsync();

			var costById =
				items
					.GroupBy(x => x.ProductId)
					.ToDictionary(
						x => x.Key,
						x => x.Sum(y => y.TotalCost));

			var quantityById =
				items
					.GroupBy(x => x.ProductId)
					.ToDictionary(
						x => x.Key,
						x => x.Sum(y => y.QuantityBaseUnit));

			var revenueById =
				revenueItems
					.GroupBy(x => x.ProductId)
					.ToDictionary(
						x => x.Key,
						x => x.Sum(y => y.Revenue));

			var productIds =
				costById.Keys
					.Union(revenueById.Keys)
					.Distinct()
					.ToList();

			foreach (var productId in productIds)
			{
				var product =
					items.FirstOrDefault(x =>
						x.ProductId == productId);

				var revenue =
					revenueById.TryGetValue(productId, out var r)
						? r
						: 0m;

				var cost =
					costById.TryGetValue(productId, out var c)
						? c
						: 0m;

				var quantity =
					quantityById.TryGetValue(productId, out var q)
						? q
						: 0m;

				var profit = revenue - cost;

				model.Rows.Add(
					new ProductProfitRowViewModel
					{
						ProductId = productId,
						ProductName = product?.ProductName ?? "-",
						ProductCode = product?.ProductCode ?? "-",
						QuantityInBaseUnit = quantity,
						Revenue = revenue,
						Cost = cost,
						Profit = profit,
						MarginPercent =
							revenue == 0
								? 0
								: Math.Round(
									profit / revenue * 100m,
									2)
					});

				model.GrandQuantity += quantity;
				model.GrandRevenue += revenue;
				model.GrandCost += cost;
				model.GrandProfit += profit;
			}

			model.Rows =
				model.Rows
					.OrderByDescending(x => x.Profit)
					.ToList();

			return View(model);
		}

		// =========================================
		// تقرير المبيعات اليومي
		// =========================================

		[HttpGet]
		[RequirePermission("report.sales")]
		public async Task<IActionResult> DailySalesReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new DailySalesReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var invoices =
				await _context.SalesInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= from &&
						x.InvoiceDate < to)
					.Select(x => new
					{
						x.InvoiceDate,
						x.TotalAmount
					})
					.ToListAsync();

			var returns =
				await _context.SalesReturnInvoices
					.AsNoTracking()
					.Where(x =>
						x.ReturnDate >= from &&
						x.ReturnDate < to)
					.Select(x => new
					{
						x.ReturnDate,
						x.TotalAmount
					})
					.ToListAsync();

			var salesByDay =
				invoices
					.GroupBy(x => x.InvoiceDate.Date)
					.ToDictionary(
						x => x.Key,
						x => new
						{
							Count = x.Count(),
							Total = x.Sum(y => y.TotalAmount)
						});

			var returnsByDay =
				returns
					.GroupBy(x => x.ReturnDate.Date)
					.ToDictionary(
						x => x.Key,
						x => x.Sum(y => y.TotalAmount));

			var days =
				salesByDay.Keys
					.Union(returnsByDay.Keys)
					.OrderBy(x => x);

			foreach (var day in days)
			{
				salesByDay.TryGetValue(
					day,
					out var sale);

				returnsByDay.TryGetValue(
					day,
					out var returned);

				var sales =
					sale?.Total ?? 0m;

				var count =
					sale?.Count ?? 0;

				var row =
					new DailySalesRowViewModel
					{
						Date = day,
						InvoiceCount = count,
						Sales = sales,
						Returns = returned,
						Net = sales - returned
					};

				model.Rows.Add(row);

				model.GrandSales += sales;
				model.GrandReturns += returned;
				model.InvoiceCount += count;
			}

			model.GrandNet =
				model.GrandSales - model.GrandReturns;

			return View(model);
		}

		// =========================================
		// أدوات مساعدة
		// =========================================

		private DateTime BuildDefaultFrom()
		{
			return new DateTime(
				DateTime.Today.Year,
				DateTime.Today.Month,
				1);
		}

		private static decimal EffectiveCreditAmount(
			TreasuryTransaction payment)
		{
			if (payment.ApplyWalletIncomeCommission &&
				payment.WalletCommissionAmount.HasValue)
			{
				return Math.Max(
					0m,
					payment.Amount -
					payment.WalletCommissionAmount.Value);
			}

			return payment.Amount;
		}

		private async Task LoadCustomersAsync(
			CustomerStatementViewModel model)
		{
			model.Customers =
				await _context.Customers
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();
		}

		private async Task LoadSuppliersAsync(
			SupplierStatementViewModel model)
		{
			model.Suppliers =
				await _context.Suppliers
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();
		}

		// =========================================
		// تقرير ربحية الفاتورة
		// =========================================

		[HttpGet]
		[RequirePermission("report.invoiceprofit")]
		public async Task<IActionResult> InvoiceProfitReport(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new InvoiceProfitReportViewModel
				{
					FromDate = fromDate ?? BuildDefaultFrom(),
					ToDate = toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var invoices =
				await _context.SalesInvoices
					.AsNoTracking()
					.Include(x => x.Customer)
					.Include(x => x.Items)
						.ThenInclude(i => i.Product)
					.Include(x => x.Items)
						.ThenInclude(i => i.LotAllocations)
					.Where(x =>
						x.Status == SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= from &&
						x.InvoiceDate < to)
					.OrderBy(x => x.InvoiceDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			foreach (var invoice in invoices)
			{
				var row =
					new InvoiceProfitRowViewModel
					{
						InvoiceNumber = invoice.InvoiceNumber,
						InvoiceDate = invoice.InvoiceDate,
						CustomerName =
							invoice.Customer?.Name ?? "نقدي",
						GrossSales = invoice.SubTotal,
						Discount = invoice.DiscountAmount,
						NetSales =
							invoice.SubTotal - invoice.DiscountAmount
					};

				decimal invoiceCOGS = 0;

				foreach (var item in invoice.Items)
				{
					decimal itemCost =
						item.LotAllocations.Sum(l => l.TotalCost);
					invoiceCOGS += itemCost;

					decimal itemNetSale = item.TotalPrice;
					if (invoice.SubTotal > 0 && invoice.DiscountAmount > 0)
					{
						itemNetSale =
							item.TotalPrice -
							(invoice.DiscountAmount *
								item.TotalPrice / invoice.SubTotal);
					}

					row.Items.Add(
						new InvoiceProfitItemRowViewModel
						{
							ProductName =
								item.Product?.Name ?? "غير معروف",
							Quantity = item.Quantity,
							NetSale = itemNetSale,
							Cost = itemCost,
							Profit = itemNetSale - itemCost
						});
				}

				row.COGS = invoiceCOGS;
				row.Profit = row.NetSales - invoiceCOGS;
				row.MarginPercent =
					row.NetSales > 0
						? row.Profit / row.NetSales * 100
						: 0;

				model.Rows.Add(row);

				model.TotalGrossSales += row.GrossSales;
				model.TotalDiscounts += row.Discount;
				model.TotalNetSales += row.NetSales;
				model.TotalCOGS += row.COGS;
				model.TotalProfit += row.Profit;
			}

			model.InvoiceCount = model.Rows.Count;
			model.OverallMarginPercent =
				model.TotalNetSales > 0
					? model.TotalProfit / model.TotalNetSales * 100
					: 0;

			return View(model);
		}
	}
}