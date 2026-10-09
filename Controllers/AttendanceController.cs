using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class AttendanceController : BaseController
	{
		public AttendanceController(ApplicationDbContext context) : base(context) { }

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
		public async Task<IActionResult> Index(string? search, int? employeeId, string? status)
		{
			var query = _context.Attendances
				.AsNoTracking()
				.Include(x => x.Employee)
				.AsQueryable();

			if (!string.IsNullOrWhiteSpace(search))
			{
				search = search.Trim();
				query = query.Where(x => x.Employee != null && x.Employee.Name.Contains(search));
			}

			if (employeeId.HasValue && employeeId.Value > 0)
				query = query.Where(x => x.EmployeeId == employeeId.Value);

			if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AttendanceStatus>(status, out var statusEnum))
				query = query.Where(x => x.Status == statusEnum);

			var records = await query
				.OrderByDescending(x => x.Date)
				.ToListAsync();

			ViewBag.Employees = await _context.Employees
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();

			ViewBag.Search = search;
			ViewBag.EmployeeId = employeeId;
			ViewBag.Status = status;

			return View(records);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.edit")]
		public async Task<IActionResult> CheckIn(int employeeId)
		{
			var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == employeeId);
			if (employee == null) return NotFound();

			var today = DateTime.Today;

			var existing = await _context.Attendances
				.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == today);

			if (existing != null && existing.CheckIn.HasValue)
				return this.RedirectWithError(nameof(Index), "تم تسجيل الحضور لهذا الموظف اليوم بالفعل.");

			if (existing == null)
			{
				_context.Attendances.Add(new Attendance
				{
					EmployeeId = employeeId,
					Date = today,
					CheckIn = DateTime.Now,
					Status = AttendanceStatus.Present,
					CreatedAt = DateTime.UtcNow
				});
			}
			else
			{
				existing.CheckIn = DateTime.Now;
				existing.Status = AttendanceStatus.Present;
			}

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), $"تم تسجيل حضور {employee.Name} بنجاح.");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.edit")]
		public async Task<IActionResult> CheckOut(int employeeId)
		{
			var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == employeeId);
			if (employee == null) return NotFound();

			var today = DateTime.Today;

			var record = await _context.Attendances
				.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == today);

			if (record == null || !record.CheckIn.HasValue)
				return this.RedirectWithError(nameof(Index), "لم يتم تسجيل الحضور لهذا الموظف اليوم.");

			if (record.CheckOut.HasValue)
				return this.RedirectWithError(nameof(Index), "تم تسجيل الانصراف لهذا الموظف اليوم بالفعل.");

			record.CheckOut = DateTime.Now;
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), $"تم تسجيل انصراف {employee.Name} بنجاح.");
		}

		[HttpGet]
		public async Task<IActionResult> MyAttendance()
		{
			var employeeId = await GetCurrentEmployeeIdAsync();
			if (employeeId == null)
				return RedirectToAction("AccessDenied", "Account");

			var records = await _context.Attendances
				.AsNoTracking()
				.Where(x => x.EmployeeId == employeeId.Value)
				.OrderByDescending(x => x.Date)
				.ToListAsync();

			return View(records);
		}
	}
}
