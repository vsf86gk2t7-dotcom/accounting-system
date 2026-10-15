using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class LeaveRequestController : BaseController
	{
		public LeaveRequestController(ApplicationDbContext context) : base(context) { }

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
		public async Task<IActionResult> Index(string? status)
		{
			var query = _context.LeaveRequests
				.AsNoTracking()
				.Include(x => x.Employee)
				.Include(x => x.ApprovedByUser)
				.AsQueryable();

			if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, out var statusEnum))
				query = query.Where(x => x.Status == statusEnum);

			var requests = await query
				.OrderByDescending(x => x.CreatedAt)
				.ToListAsync();

			ViewBag.Status = status;

			return View(requests);
		}

		[HttpGet]
		public async Task<IActionResult> Create()
		{
			var employeeId = await GetCurrentEmployeeIdAsync();
			if (employeeId == null)
				return RedirectToAction("AccessDenied", "Account");

			return View(new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(LeaveRequest model)
		{
			var employeeId = await GetCurrentEmployeeIdAsync();
			if (employeeId == null)
				return RedirectToAction("AccessDenied", "Account");

			model.EmployeeId = employeeId.Value;
			model.Status = LeaveStatus.Pending;

			if (model.EndDate < model.StartDate)
			{
				ModelState.AddModelError(nameof(model.EndDate), "تاريخ النهاية يجب أن يكون بعد تاريخ البداية.");
			}

			if (!ModelState.IsValid)
				return View(model);

			_context.LeaveRequests.Add(new LeaveRequest
			{
				EmployeeId = model.EmployeeId,
				StartDate = model.StartDate,
				EndDate = model.EndDate,
				Reason = model.Reason,
				Status = LeaveStatus.Pending,
				CreatedAt = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(MyLeaves), "تم إرسال طلب الإجازة بنجاح.");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.edit")]
		public async Task<IActionResult> Approve(int id)
		{
			var request = await _context.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id);
			if (request == null) return NotFound();

			if (request.Status != LeaveStatus.Pending)
				return this.RedirectWithError(nameof(Index), "هذا الطلب تمت معالجته مسبقاً.");

			var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
			int.TryParse(userIdValue, out var userId);

			request.Status = LeaveStatus.Approved;
			request.ApprovedBy = userId;

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تمت الموافقة على طلب الإجازة.");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.edit")]
		public async Task<IActionResult> Reject(int id)
		{
			var request = await _context.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id);
			if (request == null) return NotFound();

			if (request.Status != LeaveStatus.Pending)
				return this.RedirectWithError(nameof(Index), "هذا الطلب تمت معالجته مسبقاً.");

			var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
			int.TryParse(userIdValue, out var userId);

			request.Status = LeaveStatus.Rejected;
			request.ApprovedBy = userId;

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم رفض طلب الإجازة.");
		}

		[HttpGet]
		public async Task<IActionResult> MyLeaves()
		{
			var employeeId = await GetCurrentEmployeeIdAsync();
			if (employeeId == null)
				return RedirectToAction("AccessDenied", "Account");

			var requests = await _context.LeaveRequests
				.AsNoTracking()
				.Include(x => x.ApprovedByUser)
				.Where(x => x.EmployeeId == employeeId.Value)
				.OrderByDescending(x => x.CreatedAt)
				.ToListAsync();

			return View(requests);
		}
	}
}
