using AccountingSystem.Data;
using AccountingSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	[Authorize]
	public class ProfileController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IWebHostEnvironment _env;
		private readonly ILogger<ProfileController> _logger;

		private const long MaxImageSize = 2_000_000; // 2 MB

		private static readonly string[] AllowedExtensions =
			{ ".jpg", ".jpeg", ".png", ".webp" };

		public ProfileController(
			ApplicationDbContext context,
			IWebHostEnvironment env,
			ILogger<ProfileController> logger)
		{
			_context = context;
			_env = env;
			_logger = logger;
		}

		// =========================================
		// استخراج UserId من الـ Claims
		// =========================================

		private int GetCurrentUserId()
		{
			var idStr =
				User.FindFirstValue(ClaimTypes.NameIdentifier);

			return int.TryParse(idStr, out var id) ? id : 0;
		}

		// =========================================
		// GET: /Profile/Edit
		// =========================================

		[HttpGet]
		public async Task<IActionResult> Edit()
		{
			var userId = GetCurrentUserId();

			if (userId == 0)
			{
				return RedirectToAction(
					"Login", "Account");
			}

			var user = await _context.Users
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == userId);

			if (user == null)
			{
				return NotFound();
			}

			var vm = new ProfileViewModel
			{
				FullName = user.FullName ?? "",
				Phone = user.Phone,
				Address = user.Address,
				BirthDate = user.BirthDate,
				ExistingImagePath = user.ProfileImagePath
			};

			return View(vm);
		}

		// =========================================
		// POST: /Profile/Edit
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequestSizeLimit(2_000_000)]
		public async Task<IActionResult> Edit(
			ProfileViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var userId = GetCurrentUserId();

			if (userId == 0)
			{
				return RedirectToAction(
					"Login", "Account");
			}

			var user = await _context.Users
				.FirstOrDefaultAsync(x => x.Id == userId);

			if (user == null)
			{
				return NotFound();
			}

			// =====================================
			// رفع الصورة (اختياري)
			// =====================================

			if (model.ImageFile != null &&
				model.ImageFile.Length > 0)
			{
				var validationError =
					ValidateImage(model.ImageFile);

				if (validationError != null)
				{
					ModelState.AddModelError(
						nameof(model.ImageFile),
						validationError);

					model.ExistingImagePath =
						user.ProfileImagePath;

					return View(model);
				}

				var ext =
					Path.GetExtension(
						model.ImageFile.FileName)
						.ToLowerInvariant();

				var folder = Path.Combine(
					_env.WebRootPath,
					"uploads",
					"profiles");

				Directory.CreateDirectory(folder);

				var fileName =
					$"{Guid.NewGuid():N}{ext}";

				var filePath =
					Path.Combine(folder, fileName);

				using (var stream =
					new FileStream(
						filePath,
						FileMode.Create))
				{
					await model.ImageFile
						.CopyToAsync(stream);
				}

				// احذف الصورة القديمة
				DeleteOldImage(user.ProfileImagePath);

				user.ProfileImagePath =
					"/uploads/profiles/" + fileName;
			}

			// =====================================
			// تحديث البيانات
			// =====================================

			user.FullName =
				model.FullName.Trim();

			user.Address =
				string.IsNullOrWhiteSpace(model.Address)
					? null
					: model.Address.Trim();

			user.BirthDate =
				model.BirthDate;

			await _context.SaveChangesAsync();

			_logger.LogInformation(
				"User {UserId} updated profile",
				userId);

			TempData["Success"] =
				"تم حفظ البيانات بنجاح";

			return RedirectToAction(nameof(Edit));
		}

		// =========================================
		// Validation Helpers
		// =========================================

		private static string? ValidateImage(
			IFormFile file)
		{
			var ext = Path.GetExtension(file.FileName)
				.ToLowerInvariant();

			if (!AllowedExtensions.Contains(ext))
			{
				return "يُسمح فقط بـ jpg, jpeg, png, webp";
			}

			if (file.Length > MaxImageSize)
			{
				return "حجم الصورة يجب أن يكون أقل من 2 ميجا";
			}

			// فحص magic bytes
			using var stream = file.OpenReadStream();
			var header = new byte[8];

			if (stream.Read(header, 0, 8) < 4)
			{
				return "الملف غير صالح";
			}

			// JPEG
			if (header[0] == 0xFF &&
				header[1] == 0xD8 &&
				header[2] == 0xFF)
			{
				return null;
			}

			// PNG
			if (header[0] == 0x89 &&
				header[1] == 0x50 &&
				header[2] == 0x4E &&
				header[3] == 0x47)
			{
				return null;
			}

			// WebP (RIFF....WEBP)
			if (header[0] == 0x52 &&
				header[1] == 0x49 &&
				header[2] == 0x46 &&
				header[3] == 0x46)
			{
				return null;
			}

			return "الملف ليس صورة صالحة";
		}

		private void DeleteOldImage(string? path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			try
			{
				var fullPath = Path.Combine(
					_env.WebRootPath,
					path.TrimStart('/')
						.Replace('/',
							Path.DirectorySeparatorChar));

				if (System.IO.File.Exists(fullPath))
				{
					System.IO.File.Delete(fullPath);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(
					ex,
					"Failed to delete old profile image: {Path}",
					path);
			}
		}
	}
}