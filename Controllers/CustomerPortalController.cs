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
	public class CustomerPortalController : Controller
	{
		private readonly ApplicationDbContext _context;

		public CustomerPortalController(
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
			var customer = await GetCurrentCustomerAsync();

			if (customer == null)
			{
				return RedirectToAction(
					"Login",
					"Account");
			}

			// =====================================
			// ��� ������ ����� �������
			// =====================================

			var invoices = await _context.SalesInvoices
				.AsNoTracking()
				.Include(x => x.Items)
				.Where(x =>
					x.CustomerId == customer.Id &&
					x.Status == SalesInvoiceStatus.Confirmed)
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
			// ��������� ������ �����
			// =====================================

			var totalReturns = await _context.SalesReturnInvoices
				.AsNoTracking()
				.Where(x => x.CustomerId == customer.Id)
				.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

			var totalPaid = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x =>
					x.CustomerId == customer.Id &&
					x.Type == TreasuryTransactionType.Receive)
				.SumAsync(x => (decimal?)x.Amount) ?? 0m;

			var currentBalance =
				customer.OpeningBalance +
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

			return View(customer);
		}

		// =========================================
		// ��� ���� ������
		// =========================================

		[HttpGet]
		public async Task<IActionResult> Statement()
		{
			var customer = await GetCurrentCustomerAsync();

			if (customer == null)
			{
				return RedirectToAction(
					"Login",
					"Account");
			}

			// =====================================
			// ��� ������ ����� �������
			// =====================================

			var invoices = await _context.SalesInvoices
				.AsNoTracking()
				.Where(x =>
					x.CustomerId == customer.Id &&
					x.Status == SalesInvoiceStatus.Confirmed)
				.OrderBy(x => x.InvoiceDate)
				.ThenBy(x => x.Id)
				.ToListAsync();

			// =====================================
			// ��� ���������
			// =====================================

			var returns = await _context.SalesReturnInvoices
				.AsNoTracking()
				.Where(x => x.CustomerId == customer.Id)
				.OrderBy(x => x.ReturnDate)
				.ThenBy(x => x.Id)
				.ToListAsync();

			// =====================================
			// ��� ����� �����
			// =====================================

			var payments = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x =>
					x.CustomerId == customer.Id &&
					x.Type == TreasuryTransactionType.Receive)
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
						TypeLabel = "������ ���",
						BadgeClass = "bg-primary",
						Reference = invoice.InvoiceNumber,
						Debit = invoice.TotalAmount
					});
			}

			foreach (var item in returns)
			{
				events.Add(
					new PortalStatementRow
					{
						Date = item.ReturnDate,
						TypeLabel = "����� ���",
						BadgeClass = "bg-warning text-dark",
						Reference = item.InvoiceNumber,
						Credit = item.TotalAmount
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
						Credit = payment.Amount
					});
			}

			// =====================================
			// ���� ������ ������
			// =====================================

			var model =
				new PortalStatementViewModel
				{
					PartyName = customer.Name,
					OpeningBalance = customer.OpeningBalance,
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
				balance += row.Debit - row.Credit;
				row.Balance = balance;

				model.Rows.Add(row);
			}

			model.CurrentBalance = balance;

			return View(model);
		}

		// =========================================
		// ������ ������
		// =========================================

		[HttpGet]
		public async Task<IActionResult> Invoices()
		{
			var customer = await GetCurrentCustomerAsync();

			if (customer == null)
			{
				return RedirectToAction(
					"Login",
					"Account");
			}

			// =====================================
			// ��� ������ ����� ������ �������
			// =====================================

			var invoices = await _context.SalesInvoices
				.AsNoTracking()
				.Include(x => x.Store)
				.Include(x => x.Items)
				.ThenInclude(x => x.Product)
				.Where(x =>
					x.CustomerId == customer.Id &&
					x.Status == SalesInvoiceStatus.Confirmed)
				.OrderByDescending(x => x.InvoiceDate)
				.ThenByDescending(x => x.Id)
				.ToListAsync();

			// =====================================
			// ����� �������� ��� ViewBag
			// =====================================

			ViewBag.Invoices = invoices;

			return View(customer);
		}

		// =========================================
		// Helper - ��� ������ ������ �� JWT
		// =========================================

		private async Task<Customer?> GetCurrentCustomerAsync()
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

			if (role != UserType.Customer.ToString())
			{
				return null;
			}

			// =====================================
			// ��� ��������
			// =====================================

			var user = await _context.Users
				.AsNoTracking()
				.Include(x => x.Customer)
				.FirstOrDefaultAsync(x => x.Id == userId);

			if (user == null)
			{
				return null;
			}

			// =====================================
			// ������ �� ������ ������ �����
			// =====================================

			if (user.UserType != UserType.Customer ||
				user.CustomerId == null ||
				user.Customer == null)
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

			return user.Customer;
		}
	}
}
