using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class CompanyController : BaseController
	{
		private readonly IWebHostEnvironment _env;

		public CompanyController(ApplicationDbContext context, IWebHostEnvironment env) : base(context)
			=> _env = env;

		private async Task<string?> SaveLogoAsync(IFormFile? file, int companyId)
		{
			if (file == null || file.Length == 0) return null;

			if (file.Length > AppConstants.MaxLogoSizeBytes)
			{
				ModelState.AddModelError("", "حجم الشعار يجب ألا يزيد عن 2 ميجابايت.");
				return null;
			}

			var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
			var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg" };

			if (!allowed.Contains(ext))
			{
				ModelState.AddModelError("", "نوع الملف يجب أن يكون من الصيغ PNG/JPG/GIF/WebP/SVG فقط.");
				return null;
			}

			var folder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "logos");
			Directory.CreateDirectory(folder);

			var fileName = $"company-{companyId}{ext}";
			using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
				await file.CopyToAsync(stream);

			return $"/uploads/logos/{fileName}";
		}

		[HttpGet, RequirePermission("company.view")]
		public async Task<IActionResult> Index()
			=> View(await _context.Companies.AsNoTracking()
				.OrderBy(x => x.Id).FirstOrDefaultAsync());

		[HttpGet, RequirePermission("company.create")]
		public async Task<IActionResult> Create()
		{
			if (await _context.Companies.AnyAsync())
				return this.RedirectWithError(nameof(Index), "لا يمكن إنشاء أكثر من شركة.");
			return View();
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("company.create")]
		[RequestSizeLimit(2_000_000)]
		public async Task<IActionResult> Create(Company company, IFormFile? logo)
		{
			if (!ModelState.IsValid) return View(company);

			if (await _context.Companies.AnyAsync())
				return this.RedirectWithError(nameof(Index), "لا يمكن إنشاء أكثر من شركة.");

			company.Name = company.Name.Trim();
			company.CreatedAt = DateTime.UtcNow;
			company.IsActive = true;

			_context.Companies.Add(company);
			await _context.SaveChangesAsync();

			var logoPath = await SaveLogoAsync(logo, company.Id);
			if (logoPath != null)
			{
				company.LogoPath = logoPath;
				await _context.SaveChangesAsync();
			}

			return this.RedirectWithSuccess(nameof(Index), "تم إنشاء الشركة بنجاح.");
		}

		[HttpGet, RequirePermission("company.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var company = await _context.Companies.FindAsync(id);
			return company == null ? NotFound() : View(company);
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("company.edit")]
		[RequestSizeLimit(2_000_000)]
		public async Task<IActionResult> Edit(int id, Company model, IFormFile? logo)
		{
			if (id != model.Id) return BadRequest();
			if (!ModelState.IsValid) return View(model);

			var company = await _context.Companies.FindAsync(id);
			if (company == null) return NotFound();

			company.Name = model.Name.Trim();
			company.Phone = model.Phone;
			company.Email = model.Email;
			company.Address = model.Address;
			company.TaxNumber = model.TaxNumber;
			company.CommercialRegister = model.CommercialRegister;

			var logoPath = await SaveLogoAsync(logo, company.Id);
			if (logoPath != null) company.LogoPath = logoPath;

			await _context.SaveChangesAsync();
			return this.RedirectWithSuccess(nameof(Index), "تم تعديل بيانات الشركة بنجاح.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("company.activate")]
		public Task<IActionResult> ToggleActive(int id)
			=> ToggleActiveAsync(id, _context.Companies, "الشركة");

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("company.delete")]
		public Task<IActionResult> Delete(int id)
			=> DeleteAsync(
				id,
				_context.Companies,
				"الشركة",
				async c => await _context.Branches.AnyAsync(b => b.CompanyId == c.Id),
				"لا يمكن حذف الشركة لوجود فروع مرتبطة بها. يجب إزالة الفروع أولاً.");
	}
}