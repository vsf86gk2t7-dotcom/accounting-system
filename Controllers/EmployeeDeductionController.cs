using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class EmployeeDeductionController : BaseController
	{
		public EmployeeDeductionController(ApplicationDbContext context) : base(context) { }

		private async Task<int?> GetCurrentEmployeeIdAsync()
		{
			var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (!int.TryParse(userIdValue, out var userId)) return null;

			var user = await _context.Users
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == userId);

			return user?.EmployeeId;
		}

		[HttpGet]
		[RequirePermission("employee.view")]
		public async Task<IActionResult> Index(string? search, int? employeeId)
		{
			var query = _context.EmployeeDeductions
				.AsNoTracking()
				.Include(x => x.Employee)
				.AsQueryable();

			if (!string.IsNullOrWhiteSpace(search))
			{
				search = search.Trim();
				query = query.Where(x =>
					x.Employee != null && x.Employee.Name.Contains(search) ||
					x.Reason.Contains(search));
			}

			if (employeeId.HasValue && employeeId.Value > 0)
				query = query.Where(x => x.EmployeeId == employeeId.Value);

			var deductions = await query
				.OrderByDescending(x => x.Date)
				.ToListAsync();

			ViewBag.Employees = await _context.Employees
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();

			ViewBag.Search = search;
			ViewBag.EmployeeId = employeeId;

			return View(deductions);
		}

		[HttpGet]
		[RequirePermission("employee.edit")]
		public async Task<IActionResult> Create()
		{
			var employees = await _context.Employees
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();

			ViewBag.Employees = employees;

			return View(new EmployeeDeduction { Date = DateTime.Today });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.edit")]
				public async Task<IActionResult> Create(EmployeeDeduction model)
		{
			if (!ModelState.IsValid)
			{
				ViewBag.Employees = await _context.Employees
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.ToListAsync();

				return View(model);
			}

			// ✅ Validation: الموظف موجود ونشط
			var employeeExists = await _context.Employees
				.AsNoTracking()
				.AnyAsync(x => x.Id == model.EmployeeId && x.IsActive);

			if (!employeeExists)
			{
				ModelState.AddModelError(
					nameof(model.EmployeeId),
					"الموظف غير موجود أو غير نشط.");

				ViewBag.Employees = await _context.Employees
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.ToListAsync();

				return View(model);
			}

			_context.EmployeeDeductions.Add(new EmployeeDeduction
			{
				EmployeeId = model.EmployeeId,
				Amount = model.Amount,
				Reason = model.Reason,
				Date = model.Date,
				CreatedAt = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم إضافة الخصم بنجاح.");
		}

		[HttpGet]
		public async Task<IActionResult> MyDeductions()
		{
			var employeeId = await GetCurrentEmployeeIdAsync();

			// هل المستخدم أدمن؟
			var roleCode = User.FindFirst("Role")?.Value
				?? User.FindFirst(ClaimTypes.Role)?.Value ?? "";

			var isAdmin = roleCode.Equals("Admin", StringComparison.OrdinalIgnoreCase);

			// موظف عادي بدون حساب مرتبط → ممنوع
			if (!employeeId.HasValue && !isAdmin)
				return RedirectToAction("AccessDenied", "Account");

			var query = _context.EmployeeDeductions
				.AsNoTracking()
				.Include(x => x.Employee)
				.AsQueryable();

			// الموظف يرى خصوماته فقط
			if (employeeId.HasValue)
			{
				query = query.Where(x => x.EmployeeId == employeeId.Value);
			}
			// الأدمن: بدون filter → يرى الكل

			var deductions = await query
				.OrderByDescending(x => x.Date)
				.ToListAsync();

			// نمرر العلم للـ View عشان يعرض عمود الموظف للأدمن
			ViewBag.ShowEmployeeColumn = !employeeId.HasValue && isAdmin;
			ViewBag.IsAdminView = !employeeId.HasValue && isAdmin;

			return View(deductions);
		}
	}
}
