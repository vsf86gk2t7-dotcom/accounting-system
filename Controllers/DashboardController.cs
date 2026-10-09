using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class DashboardController : Controller
	{
		private readonly ApplicationDbContext _context;

		public DashboardController(ApplicationDbContext context)
		{
			_context = context;
		}

		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var today = DateTime.Today;
			var firstOfMonth = new DateTime(today.Year, today.Month, 1);
			var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

			// === 1. Summary Stats ===
			var grossSales =
				await _context.SalesInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status == SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= firstOfMonth &&
						x.InvoiceDate <= lastOfMonth)
					.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

			var discounts =
				await _context.SalesInvoices
					.AsNoTracking()
					.Where(x =>
						x.Status == SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= firstOfMonth &&
						x.InvoiceDate <= lastOfMonth)
					.SumAsync(x => (decimal?)x.DiscountAmount) ?? 0m;

			var salesReturns =
				await _context.SalesReturnInvoices
					.AsNoTracking()
					.Where(x =>
						x.ReturnDate >= firstOfMonth &&
						x.ReturnDate <= lastOfMonth)
					.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

			var netSales = grossSales - discounts - salesReturns;

			var costOfGoodsSold =
				await _context.SalesInvoiceItemLots
					.AsNoTracking()
					.Include(x => x.SalesInvoiceItem)
					.ThenInclude(x => x!.SalesInvoice)
					.Where(x =>
						x.SalesInvoiceItem!.SalesInvoice!.Status ==
							SalesInvoiceStatus.Confirmed &&
						x.SalesInvoiceItem.SalesInvoice.InvoiceDate >= firstOfMonth &&
						x.SalesInvoiceItem.SalesInvoice.InvoiceDate <= lastOfMonth)
					.SumAsync(x => (decimal?)x.TotalCost) ?? 0m;

			var returnCost =
				await _context.SalesReturnItemLots
					.AsNoTracking()
					.Where(x =>
						x.SalesReturnInvoiceItem != null &&
						x.SalesReturnInvoiceItem.SalesReturnInvoice != null &&
						x.SalesReturnInvoiceItem.SalesReturnInvoice.ReturnDate >= firstOfMonth &&
						x.SalesReturnInvoiceItem.SalesReturnInvoice.ReturnDate <= lastOfMonth)
					.SumAsync(x => (decimal?)x.TotalCost) ?? 0m;

			costOfGoodsSold -= returnCost;

			var grossProfit = netSales - costOfGoodsSold;
			var grossMarginPercent = grossProfit != 0
				? Math.Round(grossProfit / netSales * 100m, 2)
				: 0m;

			// === 2. Loss Analysis ===
			var invoices =
				await _context.SalesInvoices
					.AsNoTracking()
					.Include(x => x.Customer)
					.Include(x => x.Items)
						.ThenInclude(i => i.Product)
					.Where(x =>
						x.Status == SalesInvoiceStatus.Confirmed &&
						x.InvoiceDate >= firstOfMonth &&
						x.InvoiceDate <= lastOfMonth)
					.ToListAsync();

			var lossAnalysis = new List<LossInvoiceViewModel>();

			foreach (var invoice in invoices)
			{
				decimal invoiceNetSales = invoice.SubTotal - invoice.DiscountAmount;
				decimal invoiceCOGS = 0;

				foreach (var item in invoice.Items)
				{
					decimal itemCost = item.LotAllocations.Sum(l => l.TotalCost);
					invoiceCOGS += itemCost;

					decimal itemNetSale = item.TotalPrice;
					if (invoice.SubTotal > 0 && invoice.DiscountAmount > 0)
					{
						itemNetSale =
							item.TotalPrice -
							(invoice.DiscountAmount * item.TotalPrice / invoice.SubTotal);
					}

					decimal profit = itemNetSale - itemCost;

					if (profit < 0)
					{
						lossAnalysis.Add(new LossInvoiceViewModel
						{
							InvoiceNumber = invoice.InvoiceNumber,
							InvoiceDate = invoice.InvoiceDate,
							CustomerName = invoice.Customer?.Name ?? "نقدي",
							GrossSale = invoice.SubTotal,
							Discount = invoice.DiscountAmount,
							NetSale = invoiceNetSales,
							COGS = invoiceCOGS,
							Profit = invoiceNetSales - invoiceCOGS,
							MarginPercent = invoiceNetSales > 0
								? Math.Round((invoiceNetSales - invoiceCOGS) / invoiceNetSales * 100m, 2)
								: -100m,
							ProductCount = invoice.Items.Count
						});
					}
				}
			}

			// Loss reasons
			var lossReasons = new Dictionary<string, decimal>();
			foreach (var loss in lossAnalysis)
			{
				if (loss.NetSale < loss.COGS)
				{
					var key = "بيع أقل من التكلفة";
					lossReasons[key] = lossReasons.TryGetValue(key, out var ex) ? ex + Math.Abs(loss.Profit) : Math.Abs(loss.Profit);
				}
				else
				{
					var key = "خصومات مرتفعة";
					lossReasons[key] = lossReasons.TryGetValue(key, out var ex) ? ex + Math.Abs(loss.Profit) : Math.Abs(loss.Profit);
				}
			}

			var totalLossAmount = lossAnalysis.Sum(x => x.Profit);

			var model = new DashboardViewModel
			{
				GrossSales = grossSales,
				Discounts = discounts,
				SalesReturns = salesReturns,
				NetSales = netSales,
				CostOfGoodsSold = costOfGoodsSold,
				GrossProfit = grossProfit,
				GrossMarginPercent = grossMarginPercent,
				LossInvoiceCount = lossAnalysis.Count,
				TotalLossAmount = Math.Abs(totalLossAmount),
				LossReasons = lossReasons,
				LossInvoices = lossAnalysis.Take(10).ToList(),
				FromDate = firstOfMonth,
				ToDate = lastOfMonth
			};

			return View(model);
		}

		[HttpGet]
		public async Task<IActionResult> LossInvoiceDetails(string invoiceNumber)
		{
			var invoice =
				await _context.SalesInvoices
					.AsNoTracking()
					.Include(x => x.Customer)
					.Include(x => x.Items)
						.ThenInclude(i => i.Product)
					.Include(x => x.Items)
						.ThenInclude(i => i.LotAllocations)
					.FirstOrDefaultAsync(x => x.InvoiceNumber == invoiceNumber);

			if (invoice == null)
			{
				return NotFound();
			}

			var items = new List<DashboardItemViewModel>();

			foreach (var item in invoice.Items)
			{
				decimal itemCost = item.LotAllocations.Sum(l => l.TotalCost);
				decimal itemNetSale = item.TotalPrice;
				if (invoice.SubTotal > 0 && invoice.DiscountAmount > 0)
				{
					itemNetSale =
						item.TotalPrice -
						(invoice.DiscountAmount * item.TotalPrice / invoice.SubTotal);
				}

				items.Add(new DashboardItemViewModel
				{
					ProductName = item.Product?.Name ?? "غير معروف",
					Quantity = item.Quantity,
					UnitPrice = item.TotalPrice / item.Quantity,
					NetSale = itemNetSale,
					Cost = itemCost,
					Profit = itemNetSale - itemCost,
					MarginPercent = itemNetSale > 0
						? Math.Round((itemNetSale - itemCost) / itemNetSale * 100m, 2)
						: -100m
				});
			}

			var model = new LossInvoiceDetailsViewModel
			{
				InvoiceNumber = invoice.InvoiceNumber,
				InvoiceDate = invoice.InvoiceDate,
				CustomerName = invoice.Customer?.Name ?? "نقدي",
				SubTotal = invoice.SubTotal,
				DiscountAmount = invoice.DiscountAmount,
				NetSales = invoice.SubTotal - invoice.DiscountAmount,
				TotalCOGS = invoice.Items.Sum(i => i.LotAllocations.Sum(l => l.TotalCost)),
				TotalProfit = (invoice.SubTotal - invoice.DiscountAmount) - invoice.Items.Sum(i => i.LotAllocations.Sum(l => l.TotalCost)),
				Items = items
			};

			return View(model);
		}
	}
}