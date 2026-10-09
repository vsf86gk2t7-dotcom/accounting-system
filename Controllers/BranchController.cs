using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class BranchController : BaseController
	{
		public BranchController(ApplicationDbContext context) : base(context) { }

		[HttpGet, RequirePermission("branch.view")]
		public async Task<IActionResult> Index()
			=> View(await _context.Branches.AsNoTracking()
				.Include(x => x.Company).OrderBy(x => x.Name).ToListAsync());

		[HttpGet, RequirePermission("branch.create")]
		public async Task<IActionResult> Create()
		{
			if (!await _context.Companies.AnyAsync())
				return this.RedirectWithError("Index", "Company",
					"يجب إنشاء الشركة أولاً قبل إنشاء الفروع.");
			return View();
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("branch.create")]
		public async Task<IActionResult> Create(Branch branch)
		{
			if (!ModelState.IsValid) return View(branch);

			var company = await _context.Companies.FirstOrDefaultAsync();
			if (company == null)
				return this.RedirectWithError("Index", "Company",
					"لا توجد شركة مسجلة في النظام.");

			branch.CompanyId = company.Id;
			branch.Name = branch.Name.Trim();
			branch.CreatedAt = DateTime.UtcNow;
			branch.IsActive = true;

			if (await _context.Branches.AnyAsync(x =>
					x.CompanyId == branch.CompanyId && x.Name == branch.Name))
			{
				ModelState.AddModelError("Name", "هذا الاسم موجود بالفعل.");
				return View(branch);
			}

			_context.Branches.Add(branch);
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم إنشاء الفرع بنجاح.");
		}

		[HttpGet, RequirePermission("branch.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var branch = await _context.Branches.FindAsync(id);
			return branch == null ? NotFound() : View(branch);
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("branch.edit")]
		public async Task<IActionResult> Edit(int id, Branch model)
		{
			if (id != model.Id) return BadRequest();
			if (!ModelState.IsValid) return View(model);

			var branch = await _context.Branches.FindAsync(id);
			if (branch == null) return NotFound();

			// ✅ Refactor: SetValues مع حماية الحقول الحساسة
			var protectedValues = new
			{
				branch.CompanyId,
				branch.IsActive,
				branch.CreatedAt
			};

			_context.Entry(branch).CurrentValues.SetValues(model);
			branch.Name = model.Name.Trim();   // Trim بعد SetValues

			// رجّع الحقول المحمية
			branch.CompanyId = protectedValues.CompanyId;
			branch.IsActive = protectedValues.IsActive;
			branch.CreatedAt = protectedValues.CreatedAt;

			await _context.SaveChangesAsync();
			return this.RedirectWithSuccess(nameof(Index), "تم تعديل الفرع بنجاح.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("branch.activate")]
		public Task<IActionResult> ToggleActive(int id)
			=> ToggleActiveAsync(id, _context.Branches, "الفرع");

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("branch.delete")]
		public Task<IActionResult> Delete(int id)
			=> DeleteAsync(
				id,
				_context.Branches,
				"الفرع",
				async b => await _context.Stores.AnyAsync(s => s.BranchId == b.Id),
				"لا يمكن حذف الفرع لوجود مخازن مرتبطة به. يجب إزالة المخازن أولاً.");
	}
}