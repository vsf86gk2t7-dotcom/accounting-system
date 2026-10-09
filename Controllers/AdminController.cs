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
		private readonly IWebHostEnvironment _environment;

		public AdminController(
			ApplicationDbContext context,
			IConfiguration configuration,
			IWebHostEnvironment environment)
		{
			_context = context;
			_configuration = configuration;
			_environment = environment;
		}

		// =====================================
		// Helpers — قراءة موحدة للإعدادات
		// =====================================

		/// <summary>
		/// مجلد النسخ الاحتياطي (مطلق دائمًا).
		/// يقرأ BackupSettings:BackupFolder، وإن كان نسبيًا يُدمج مع ContentRootPath.
		/// </summary>
		private string GetBackupFolder()
		{
			var folder = _configuration["BackupSettings:BackupFolder"];

			if (string.IsNullOrWhiteSpace(folder))
				folder = "Backups";

			if (!Path.IsPathRooted(folder))
			{
				folder = Path.Combine(
					_environment.ContentRootPath,
					folder);
			}

			return folder;
		}

		/// <summary>
		/// اسم قاعدة البيانات.
		/// الأولوية: BackupSettings:Database → الاتصال الفعلي.
		/// </summary>
		private string GetDatabaseName()
		{
			var db = _configuration["BackupSettings:Database"];

			if (!string.IsNullOrWhiteSpace(db))
				return db;

			// الأدق: من الاتصال المفتوح فعلًا
			return _context.Database.GetDbConnection().Database;
		}

		// =====================================
		// إدارة النسخ الاحتياطية
		// =====================================

		[HttpGet]
		public IActionResult Backups()
		{
			var folder = GetBackupFolder();

			var backups = new List<Models.ViewModels.Admin.BackupItem>();

			if (Directory.Exists(folder))
			{
				backups = new DirectoryInfo(folder)
					.GetFiles("*.bak")
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
			var folder = GetBackupFolder();
			var database = GetDatabaseName();

			// =====================================
			// النسخ الاحتياطي يحتاج SQL Server
			// =====================================

			if (!_context.Database.IsRelational())
			{
				TempData["Error"] =
					"النسخ الاحتياطي غير مدعوم في بيئة الاختبار (InMemory).";

				return RedirectToAction(nameof(Backups));
			}

			try
			{
				Directory.CreateDirectory(folder);

				var fileName =
					$"{database}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";

				var fullPath =
					Path.Combine(folder, fileName);

				var sql =
					$"BACKUP DATABASE [{database}] TO DISK = N'{fullPath.Replace("'", "''")}' WITH INIT, NAME = N'{fileName}'";

				await _context.Database.ExecuteSqlRawAsync(sql);

				// ✅ تحقق فعلي من وجود الملف وحجمه
				if (!System.IO.File.Exists(fullPath))
				{
					throw new InvalidOperationException(
						"تم تنفيذ BACKUP لكن الملف لم يُنشأ على القرص.");
				}

				var info = new FileInfo(fullPath);

				if (info.Length == 0)
				{
					throw new InvalidOperationException(
						"ملف النسخة موجود لكن حجمه صفر — النسخة غير صالحة.");
				}

				TempData["Success"] =
					$"تم إنشاء النسخة الاحتياطية بنجاح: {fileName} ({info.Length / 1024 / 1024} ميجا)";
			}
			catch (Exception ex)
			{
				TempData["Error"] =
					"فشل إنشاء النسخة الاحتياطية: " + ex.Message;
			}

			return RedirectToAction(nameof(Backups));
		}

		[HttpGet]
		public IActionResult DownloadBackup(string fileName)
		{
			var folder = GetBackupFolder();

			if (!FactoryResetService.TryGetSafeBackupPath(
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
			var folder = GetBackupFolder();

			if (!FactoryResetService.TryGetSafeBackupPath(
					folder,
					fileName,
					out var fullPath))
			{
				TempData["Error"] =
					"لم يتم العثور على النسخة أو مسار غير آمن.";

				return RedirectToAction(nameof(Backups));
			}

			if (System.IO.File.Exists(fullPath))
			{
				System.IO.File.Delete(fullPath);
				TempData["Success"] = "تم حذف النسخة الاحتياطية.";
			}

			return RedirectToAction(nameof(Backups));
		}

		// =====================================
		// طلبات التسجيل
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
					Type = "موظف",
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
					Type = "عميل",
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
					Type = "مورد",
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
		// طلبات استرجاع كلمة المرور
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
				"تم تحديث حالة الطلب.";

			return RedirectToAction(nameof(PasswordResetRequests));
		}
	}
}