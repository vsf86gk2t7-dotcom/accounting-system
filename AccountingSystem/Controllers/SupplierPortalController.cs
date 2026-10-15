using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	[Authorize]
	public class SupplierPortalController : Controller
	{
		private readonly ApplicationDbContext _context;

		public SupplierPortalController(
			ApplicationDbContext context)
		{
			_context = context;
		}

		// =========================================
		// ������ �������� ������ ������
		// =========================================

		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var supplier = await GetCurrentSupplierAsync();

			if (supplier == null)
			{
				return RedirectToAction(
					"Login",
					"Account");
			}

			// =====================================
			// ��� ������ ������ ���������
			// =====================================

			var invoices = await _context.PurchaseInvoices
				.AsNoTracking()
				.Include(x => x.Items)
				.Where(x =>
					x.SupplierId == supplier.Id &&
					x.Status == PurchaseInvoiceStatus.Posted)
				.ToListAsync();

			// =====================================
			// ��������
			// =====================================

			var totalInvoices = invoices.Sum(x => x.TotalAmount);
			var invoiceCount = invoices.Count;
			var totalItems = invoices.Sum(x => x.Items.Count);

			var totalProducts = invoices
				.SelectMany(x => x.Items)
				.Select(x => x.ProductId)
				.Distinct()
				.Count();

			// =====================================
			// ��������� ����� �����
			// =====================================

			var totalReturns = await _context.PurchaseReturnInvoices
				.AsNoTracking()
				.Where(x => x.SupplierId == supplier.Id)
				.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

			var totalPaid = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x =>
					x.SupplierId == supplier.Id &&
					x.Type == TreasuryTransactionType.Pay)
				.SumAsync(x => (decimal?)x.Amount) ?? 0m;

			var currentBalance =
				supplier.OpeningBalance +
				totalInvoices -
				totalReturns -
				totalPaid;

			// =====================================
			// ����� ��������
			// =====================================

			ViewBag.TotalInvoices = totalInvoices;
			ViewBag.InvoiceCount = invoiceCount;
			ViewBag.TotalItems = totalItems;
			ViewBag.TotalProducts = totalProducts;
			ViewBag.CurrentBalance = currentBalance;

			return View(supplier);
		}

		// =========================================
		// ��� ���� ������
		// =========================================

		[HttpGet]
		public async Task<IActionResult> Statement()
		{
			var supplier = await GetCurrentSupplierAsync();

			if (supplier == null)
			{
				return RedirectToAction(
					"Login",
					"Account");
			}

			// =====================================
			// ��� ������ ������ ���������
			// =====================================

			var invoices = await _context.PurchaseInvoices
				.AsNoTracking()
				.Where(x =>
					x.SupplierId == supplier.Id &&
					x.Status == PurchaseInvoiceStatus.Posted)
				.OrderBy(x => x.InvoiceDate)
				.ThenBy(x => x.Id)
				.ToListAsync();

			// =====================================
			// ��� ���������
			// =====================================

			var returns = await _context.PurchaseReturnInvoices
				.AsNoTracking()
				.Where(x => x.SupplierId == supplier.Id)
				.OrderBy(x => x.ReturnDate)
				.ThenBy(x => x.Id)
				.ToListAsync();

			// =====================================
			// ��� ���� �����
			// =====================================

			var payments = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x =>
					x.SupplierId == supplier.Id &&
					x.Type == TreasuryTransactionType.Pay)
				.OrderBy(x => x.CreatedAt)
				.ThenBy(x => x.Id)
				.ToListAsync();

			// =====================================
			// ���� ������� ������
			// =====================================

			var events = new List<PortalStatementRow>();

			foreach (var invoice in invoices)
			{
				events.Add(
					new PortalStatementRow
					{
						Date = invoice.InvoiceDate,
						TypeLabel = "������ ����",
						BadgeClass = "bg-primary",
						Reference = invoice.InvoiceNumber,
						Credit = invoice.TotalAmount
					});
			}

			foreach (var item in returns)
			{
				events.Add(
					new PortalStatementRow
					{
						Date = item.ReturnDate,
						TypeLabel = "����� ����",
						BadgeClass = "bg-warning text-dark",
						Reference = item.InvoiceNumber,
						Debit = item.TotalAmount
					});
			}

			foreach (var payment in payments)
			{
				events.Add(
					new PortalStatementRow
					{
						Date = payment.CreatedAt,
						TypeLabel = "��� ���",
						BadgeClass = "bg-success",
						Reference = payment.TransactionNumber,
						Debit = payment.Amount
					});
			}

			// =====================================
			// ���� ������ ������
			// =====================================

			var model =
				new PortalStatementViewModel
				{
					PartyName = supplier.Name,
					OpeningBalance = supplier.OpeningBalance,
					TotalInvoices =
						invoices.Sum(x => x.TotalAmount),
					TotalReturns =
						returns.Sum(x => x.TotalAmount),
					TotalPaid =
						payments.Sum(x => x.Amount)
				};

			var balance =
				model.OpeningBalance;

			foreach (var row in events.OrderBy(x => x.Date))
			{
				balance += row.Credit - row.Debit;
				row.Balance = balance;

				model.Rows.Add(row);
			}

			model.CurrentBalance = balance;

			return View(model);
		}

		// =========================================
		// ������ ������ ������ �������
		// =========================================

		[HttpGet]
		public async Task<IActionResult> Invoices()
		{
			var supplier = await GetCurrentSupplierAsync();

			if (supplier == null)
			{
				return RedirectToAction(
					"Login",
					"Account");
			}

			// =====================================
			// ��� ������ ������ ������ �������
			// =====================================

			var invoices = await _context.PurchaseInvoices
				.AsNoTracking()
				.Include(x => x.Store)
				.Include(x => x.Items)
				.Where(x =>
					x.SupplierId == supplier.Id &&
					x.Status == PurchaseInvoiceStatus.Posted)
				.OrderByDescending(x => x.InvoiceDate)
				.ThenByDescending(x => x.Id)
				.ToListAsync();

			// =====================================
			// ����� �������� ��� ViewBag
			// =====================================

			ViewBag.Invoices = invoices;

			return View(supplier);
		}

		// =========================================
		// Helper - ��� ������ ������ �� JWT
		// =========================================

		private async Task<Supplier?> GetCurrentSupplierAsync()
		{
			// =====================================
			// ������ �� ����� ������
			// =====================================

			if (User.Identity?.IsAuthenticated != true)
			{
				return null;
			}

			// =====================================
			// ����� UserId �� JWT
			// =====================================

			var userIdValue = User.FindFirstValue(
				ClaimTypes.NameIdentifier);

			if (!int.TryParse(userIdValue, out var userId))
			{
				return null;
			}

			// =====================================
			// ������ �� �������� ����
			// =====================================

			var role = User.FindFirstValue(
				ClaimTypes.Role);

			if (role != UserType.Supplier.ToString())
			{
				return null;
			}

			// =====================================
			// ��� ��������
			// =====================================

			var user = await _context.Users
				.AsNoTracking()
				.Include(x => x.Supplier)
				.FirstOrDefaultAsync(x => x.Id == userId);

			if (user == null)
			{
				return null;
			}

			// =====================================
			// ������ �� ������ ������ �����
			// =====================================

			if (user.UserType != UserType.Supplier ||
				user.SupplierId == null ||
				user.Supplier == null)
			{
				return null;
			}

			// =====================================
			// ������ �� ���� ������
			// =====================================

			if (!user.IsActive ||
				user.Status != UserStatus.Approved ||
				!user.IsPasswordSet)
			{
				return null;
			}

			return user.Supplier;
		}
	}
}
