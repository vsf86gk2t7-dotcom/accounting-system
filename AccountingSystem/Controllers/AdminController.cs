using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models.ViewModels.Admin;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	[AdminOnly]
	public class AdminController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IConfiguration _configuration;

		public AdminController(
			ApplicationDbContext context,
			IConfiguration configuration)
		{
			_context = context;
			_configuration = configuration;
		}

		// =====================================
		// «·‰”Œ «·«Õ Ì«ÿÌ…
		// =====================================

		[HttpGet]
		public IActionResult Backups()
		{
			var folder =
				_configuration["BackupSettings:Folder"];

			var backups = new List<Models.ViewModels.Admin.BackupItem>();

			if (!string.IsNullOrWhiteSpace(folder) &&
				Directory.Exists(folder))
			{
				backups = new DirectoryInfo(folder)
					.GetFiles("*.bak")
					.Where(x => x.Name.StartsWith("AccountingSystemDb_", StringComparison.OrdinalIgnoreCase))
					.OrderByDescending(x => x.LastWriteTime)
					.Select(x => new Models.ViewModels.Admin.BackupItem
					{
						FileName = x.Name,
						SizeMb = (decimal)x.Length / (1024 * 1024),
						CreatedAt = x.LastWriteTime
					})
					.ToList();
			}

			ViewBag.BackupFolder = folder;

			return View(backups);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateBackup()
		{
			var folder =
				_configuration["BackupSettings:Folder"];

			var database =
				_configuration["BackupSettings:Database"] ?? "AccountingSystemDb";

			if (string.IsNullOrWhiteSpace(folder))
			{
				TempData["Error"] = "„Ã·œ «·‰”Œ «·«Õ Ì«ÿÌ €Ì— „ı⁄œ ›Ì «·≈⁄œ«œ« .";
				return RedirectToAction(nameof(Backups));
			}

			try
			{
				Directory.CreateDirectory(folder);

				var fileName =
					$"AccountingSystemDb_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";

				var fullPath =
					Path.Combine(folder, fileName);

				// ›Õ’ √‰ Õ”«» SQL Server Ì” ÿÌ⁄ «·ﬂ «»… ›Ì «·„Ã·œ
				// „Õ«Ê·… ≈‰‘«¡ „·›  Ã—Ì»Ì ó ·« Ì„ﬂ‰  ‰›Ì– BACKUP
				// ≈·« œ«Œ· ‰›” Œ«œ„ SQL
				// SQL Express ·« Ìœ⁄„ COMPRESSION ó ‰Õ–›Â ·Ì⁄„· ⁄·Ï Â–Â «·‰”Œ…
				var sql =
					$"BACKUP DATABASE [{database}] TO DISK = N'{fullPath.Replace("'", "''")}' WITH INIT, NAME = N'{fileName}'";

				await _context.Database.ExecuteSqlRawAsync(sql);

				TempData["Success"] = $" „ ≈‰‘«¡ «·‰”Œ… «·«Õ Ì«ÿÌ… »‰Ã«Õ: {fileName}";
			}
			catch (Exception ex)
			{
				TempData["Error"] =
					"›‘·  ⁄„·Ì… «·‰”Œ «·«Õ Ì«ÿÌ: " + ex.Message;
			}

			return RedirectToAction(nameof(Backups));
		}

		[HttpGet]
		public IActionResult DownloadBackup(string fileName)
		{
			var folder =
				_configuration["BackupSettings:Folder"];

			if (!BackupFileGuard.TryGetSafeBackupPath(
					folder,
					fileName,
					out var fullPath) ||
				!System.IO.File.Exists(fullPath))
			{
				return NotFound();
			}

			var stream =
				new FileStream(fullPath, FileMode.Open, FileAccess.Read);

			return File(
				stream,
				"application/octet-stream",
				Path.GetFileName(fullPath));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult DeleteBackup(string fileName)
		{
			var folder =
				_configuration["BackupSettings:Folder"];

			if (string.IsNullOrWhiteSpace(folder))
			{
				TempData["Error"] = "„Ã·œ «·‰”Œ €Ì— „ı⁄œ.";
				return RedirectToAction(nameof(Backups));
			}

			if (!BackupFileGuard.TryGetSafeBackupPath(
					folder,
					fileName,
					out var fullPath))
			{
				TempData["Error"] = "«”„ „·› «·‰”Œ… «·«Õ Ì«ÿÌ… €Ì— ’«·Õ.";
				return RedirectToAction(nameof(Backups));
			}

			if (System.IO.File.Exists(fullPath))
			{
				System.IO.File.Delete(fullPath);
				TempData["Success"] = " „ Õ–› «·‰”Œ… «·«Õ Ì«ÿÌ….";
			}

			return RedirectToAction(nameof(Backups));
		}

		// =====================================
		// «· Õﬁﬁ „‰ ”·«„… ‰”Œ… «Õ Ì«ÿÌ…
		// =====================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> VerifyBackup(string fileName)
		{
			var folder =
				_configuration["BackupSettings:Folder"];

			if (!BackupFileGuard.TryGetSafeBackupPath(
					folder,
					fileName,
					out var fullPath) ||
				!System.IO.File.Exists(fullPath))
			{
				TempData["Error"] =
					"„·› «·‰”Œ… €Ì— „ÊÃÊœ √Ê €Ì— ’«·Õ.";

				return RedirectToAction(nameof(Backups));
			}

			try
			{
				// RESTORE VERIFYONLY Ì Õﬁﬁ „‰ ”·«„… «·„·›
				// »œÊ‰ «” —Ã«⁄Â ›⁄·Ì«.
				var sql =
					$"RESTORE VERIFYONLY FROM DISK = N'{fullPath.Replace("'", "''")}'";

				await _context.Database.ExecuteSqlRawAsync(sql);

				TempData["Success"] =
					$"? «·‰”Œ… ´{fileName}ª ”·Ì„… Êﬁ«»·… ··«” —Ã«⁄.";
			}
			catch (Exception ex)
			{
				TempData["Error"] =
					$"? «·‰”Œ… ´{fileName}ª  «·›…: {ex.Message}";
			}

			return RedirectToAction(nameof(Backups));
		}

		// =====================================
		// ÿ·»«  «· ”ÃÌ· «·„ÊÕœ…
		// =====================================

		[HttpGet]
		public async Task<IActionResult> RegistrationRequests(
			string? type)
		{
			var customers = new List<RegistrationRequestItem>();
			var suppliers = new List<RegistrationRequestItem>();
			var employees = new List<RegistrationRequestItem>();

			if (string.IsNullOrEmpty(type) ||
				type.Equals("employee", StringComparison.OrdinalIgnoreCase))
			{
				var employeeList = await _context.Employees
					.AsNoTracking()
					.Where(x => x.PortalRequested)
					.OrderByDescending(x => x.CreatedAt)
					.ToListAsync();

				employees = employeeList.Select(x => new RegistrationRequestItem
				{
					Id = x.Id,
					Type = "„ÊŸ›",
					TypeKey = "employee",
					Name = x.Name,
					Phone = x.Phone ?? "-",
					Email = x.Email,
					RequestedAt = x.CreatedAt,
					IsApproved = x.PortalApproved,
					HasAccount = x.HasPortalAccount
				}).ToList();
			}

			if (string.IsNullOrEmpty(type) ||
				type.Equals("customer", StringComparison.OrdinalIgnoreCase))
			{
				var customerList = await _context.Customers
					.AsNoTracking()
					.Where(x => x.PortalRequested)
					.OrderByDescending(x => x.CreatedAt)
					.ToListAsync();

				customers = customerList.Select(x => new RegistrationRequestItem
				{
					Id = x.Id,
					Type = "⁄„Ì·",
					TypeKey = "customer",
					Name = x.Name,
					Phone = x.Phone ?? "-",
					Email = x.Email,
					RequestedAt = x.CreatedAt,
					IsApproved = x.PortalApproved,
					HasAccount = x.HasPortalAccount
				}).ToList();
			}

			if (string.IsNullOrEmpty(type) ||
				type.Equals("supplier", StringComparison.OrdinalIgnoreCase))
			{
				var supplierList = await _context.Suppliers
					.AsNoTracking()
					.Where(x => x.PortalRequested)
					.OrderByDescending(x => x.CreatedAt)
					.ToListAsync();

				suppliers = supplierList.Select(x => new RegistrationRequestItem
				{
					Id = x.Id,
					Type = "„Ê—œ",
					TypeKey = "supplier",
					Name = x.Name,
					Phone = x.Phone ?? "-",
					Email = x.Email,
					RequestedAt = x.CreatedAt,
					IsApproved = x.PortalApproved,
					HasAccount = x.HasPortalAccount
				}).ToList();
			}

			var all = new List<RegistrationRequestItem>();
			all.AddRange(customers);
			all.AddRange(suppliers);
			all.AddRange(employees);

			all = all
				.OrderBy(x => x.IsApproved)
				.ThenByDescending(x => x.RequestedAt)
				.ToList();

			ViewBag.Type = type;

			return View(all);
		}

		// =====================================
		// ÿ·»«  «” ⁄«œ… ﬂ·„… «·„—Ê—
		// =====================================

		[HttpGet]
		public async Task<IActionResult> PasswordResetRequests(
			string? filter)
		{
			var query = _context.PasswordResetRequests
				.AsNoTracking()
				.AsQueryable();

			switch (filter)
			{
				case "pending":
					query = query.Where(x =>
						!x.IsSent &&
						!x.IsCompleted);
					break;

				case "completed":
					query = query.Where(x => x.IsCompleted);
					break;

				case "expired":
					query = query.Where(x =>
						!x.IsCompleted &&
						x.ExpiresAt < DateTime.UtcNow);
					break;

				case "sent":
					query = query.Where(x =>
						x.IsSent &&
						!x.IsCompleted);
					break;
			}

			var requests = await query
				.OrderBy(x => x.IsCompleted)
				.ThenByDescending(x => x.RequestedAt)
				.ToListAsync();

			ViewBag.Filter = filter;

			return View(requests);
		}

		// =====================================
		//  ⁄·Ì„ «·ÿ·» ﬂ„ı—”·
		// =====================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> MarkAsSent(int id)
		{
			var request = await _context.PasswordResetRequests
				.FirstOrDefaultAsync(x => x.Id == id);

			if (request == null)
			{
				return NotFound();
			}

			request.IsSent = true;
			request.SentAt = DateTime.UtcNow;

			var userIdValue = User.FindFirstValue(
				System.Security.Claims.ClaimTypes.NameIdentifier);

			if (int.TryParse(userIdValue, out var userId))
			{
				var admin = await _context.Users
					.FirstOrDefaultAsync(x => x.Id == userId);

				request.SentBy = admin?.Phone;
			}

			await _context.SaveChangesAsync();

			TempData["Success"] =
				" „  ⁄·Ì„ «·ÿ·» ﬂ„ı—”·.";

			return RedirectToAction(nameof(PasswordResetRequests));
		}
	}
}