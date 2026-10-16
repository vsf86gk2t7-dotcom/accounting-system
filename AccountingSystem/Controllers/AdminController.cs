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

		private static int _restoreInProgress = 0;

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
		// «” —Ã«⁄ ‰”Œ… «Õ Ì«ÿÌ…
		// ?? ⁄„·Ì… Œÿ—…:  ” »œ· «·»Ì«‰«  «·Õ«·Ì… »«·ﬂ«„·
		// =====================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RestoreBackup(
			string fileName,
			string confirmText)
		{
			var folder =
				_configuration["BackupSettings:Folder"];

			var database =
				_configuration["BackupSettings:Database"] ??
				"AccountingSystemDb";

			var connectionString =
				_configuration.GetConnectionString("DefaultConnection");

			if (string.IsNullOrWhiteSpace(connectionString))
			{
				TempData["Error"] =
					"Connection String €Ì— „ı⁄œ.";
				return RedirectToAction(nameof(Backups));
			}

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

			if (!string.Equals(
					confirmText?.Trim(),
					database,
					StringComparison.Ordinal))
			{
				TempData["Error"] =
					$"‰’ «· √ﬂÌœ €Ì— „ÿ«»ﬁ. ·«“„  ﬂ »: {database}";

				return RedirectToAction(nameof(Backups));
			}

			if (System.Threading.Interlocked.CompareExchange(
					ref _restoreInProgress,
					1,
					0) != 0)
			{
				TempData["Error"] =
					"⁄„·Ì… «” —Ã«⁄ √Œ—Ï Ã«—Ì… »«·›⁄·.";

				return RedirectToAction(nameof(Backups));
			}

			try
			{
				var masterBuilder =
					new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
						connectionString)
					{
						InitialCatalog = "master"
					};

				await using var conn =
					new Microsoft.Data.SqlClient.SqlConnection(
						masterBuilder.ConnectionString);

				await conn.OpenAsync();

				var escapedPath =
					fullPath.Replace("'", "''");

				await using (var verifyCmd = conn.CreateCommand())
				{
					verifyCmd.CommandText =
						$"RESTORE VERIFYONLY FROM DISK = N'{escapedPath}'";

					verifyCmd.CommandTimeout = 300;

					await verifyCmd.ExecuteNonQueryAsync();
				}

				await using (var suCmd = conn.CreateCommand())
				{
					suCmd.CommandText =
						$"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";

					suCmd.CommandTimeout = 300;

					await suCmd.ExecuteNonQueryAsync();
				}

				try
				{
					await using var restoreCmd = conn.CreateCommand();

					restoreCmd.CommandText =
						$"RESTORE DATABASE [{database}] FROM DISK = N'{escapedPath}' WITH REPLACE, RECOVERY";

					restoreCmd.CommandTimeout = 600;

					await restoreCmd.ExecuteNonQueryAsync();
				}
				finally
				{
					try
					{
						await using var muCmd = conn.CreateCommand();

						muCmd.CommandText =
							$"ALTER DATABASE [{database}] SET MULTI_USER";

						muCmd.CommandTimeout = 300;

						await muCmd.ExecuteNonQueryAsync();
					}
					catch
					{
						//  Ã«Â· ó ·Ê «·œ« «»Ì“ „‘ „ÊÃÊœ…
					}
				}

				return Content(
					@"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
	<meta charset='utf-8'>
	<title> „ «·«” —Ã«⁄</title>
	<style>
		body { font-family: 'Segoe UI', sans-serif; text-align: center; padding: 80px 20px; background: #f4f6fa; }
		.box { max-width: 600px; margin: 0 auto; background: #fff; padding: 40px; border-radius: 14px; box-shadow: 0 6px 22px rgba(15,23,42,.08); }
		h1 { color: #059669; font-size: 28px; margin-bottom: 16px; }
		p { color: #475569; font-size: 16px; line-height: 1.7; }
		.warn { background: #fef3c7; border: 1px solid #fcd34d; padding: 16px; border-radius: 10px; margin-top: 24px; color: #92400e; font-weight: 600; }
	</style>
</head>
<body>
	<div class='box'>
		<h1>?  „ «” —Ã«⁄ «·œ« «»Ì“ »‰Ã«Õ</h1>
		<p> „ «” »œ«· «·»Ì«‰«  »«·‰”Œ… «·«Õ Ì«ÿÌ… «·„Œ «—….</p>
		<div class='warn'>
			? «· ÿ»Ìﬁ „Õ «Ã ≈⁄«œ…  ‘€Ì· ÌœÊÌ«<br>
			√€·ﬁ ‰«›–… «·√Ê«„— Ê√⁄œ  ‘€Ì· «· ÿ»Ìﬁ.
		</div>
	</div>
</body>
</html>",
					"text/html");
			}
			catch (Exception ex)
			{
				TempData["Error"] =
					"›‘·  ⁄„·Ì… «·«” —Ã«⁄: " + ex.Message;

				return RedirectToAction(nameof(Backups));
			}
			finally
			{
				System.Threading.Interlocked.Exchange(
					ref _restoreInProgress,
					0);
			}
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