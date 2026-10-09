using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class StoreController : BaseController
	{
		public StoreController(ApplicationDbContext context) : base(context) { }

		private async Task LoadBranchesAsync()
			=> ViewBag.Branches = await _context.Branches.AsNoTracking()
				.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();

		[HttpGet, RequirePermission("store.view")]
		public async Task<IActionResult> Index()
			=> View(await _context.Stores.AsNoTracking()
				.Include(x => x.Branch).ThenInclude(x => x!.Company)
				.OrderBy(x => x.Branch!.Name).ThenBy(x => x.Name).ToListAsync());

		[HttpGet, RequirePermission("store.create")]
		public async Task<IActionResult> Create()
		{
			var branches = await _context.Branches.AsNoTracking()
				.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();

			if (!branches.Any())
				return this.RedirectWithError("Index", "Branch",
					"يجب إنشاء فرع واحد على الأقل أولاً.");

			ViewBag.Branches = branches;
			return View();
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("store.create")]
		public async Task<IActionResult> Create(Store store)
		{
			await LoadBranchesAsync();
			if (!ModelState.IsValid) return View(store);

			var branch = await _context.Branches.FirstOrDefaultAsync(x =>
				x.Id == store.BranchId && x.IsActive);
			if (branch == null)
			{
				ModelState.AddModelError("BranchId", "الفرع المحدد غير موجود أو غير نشط.");
				return View(store);
			}

			store.Name = store.Name.Trim();

			if (await _context.Stores.AnyAsync(x =>
					x.BranchId == store.BranchId && x.Name == store.Name))
			{
				ModelState.AddModelError("Name", "اسم المخزن موجود بالفعل داخل هذا الفرع.");
				return View(store);
			}

			store.CreatedAt = DateTime.UtcNow;
			store.IsActive = true;

			_context.Stores.Add(store);
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم إنشاء المخزن بنجاح.");
		}

		[HttpGet, RequirePermission("store.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			await LoadBranchesAsync();
			var store = await _context.Stores.FindAsync(id);
			return store == null ? NotFound() : View(store);
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("store.edit")]
		public async Task<IActionResult> Edit(int id, Store model)
		{
			await LoadBranchesAsync();
			if (id != model.Id) return BadRequest();
			if (!ModelState.IsValid) return View(model);

			var branch = await _context.Branches.FirstOrDefaultAsync(x =>
				x.Id == model.BranchId && x.IsActive);
			if (branch == null)
			{
				ModelState.AddModelError("BranchId", "الفرع المحدد غير موجود أو غير نشط.");
				return View(model);
			}

			model.Name = model.Name.Trim();

			if (await _context.Stores.AnyAsync(x =>
					x.BranchId == model.BranchId && x.Name == model.Name && x.Id != model.Id))
			{
				ModelState.AddModelError("Name", "اسم المخزن موجود بالفعل داخل هذا الفرع.");
				return View(model);
			}

			var store = await _context.Stores.FindAsync(id);
			if (store == null) return NotFound();

			// ✅ Refactor: SetValues مع حماية الحقول الحساسة
			var protectedValues = new
			{
				store.IsActive,
				store.CreatedAt
			};

			_context.Entry(store).CurrentValues.SetValues(model);

			// رجّع الحقول المحمية
			store.IsActive = protectedValues.IsActive;
			store.CreatedAt = protectedValues.CreatedAt;

			await _context.SaveChangesAsync();
			return this.RedirectWithSuccess(nameof(Index), "تم تعديل المخزن بنجاح.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("store.activate")]
		public Task<IActionResult> ToggleActive(int id)
			=> ToggleActiveAsync(id, _context.Stores, "المخزن");

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("store.delete")]
		public Task<IActionResult> Delete(int id)
			=> DeleteAsync(
				id,
				_context.Stores,
				"المخزن",
				async s => await _context.StockLots.AnyAsync(x => x.StoreId == s.Id),
				"لا يمكن حذف المخزن لوجود دفعات مخزنية مرتبطة به. يجب إزالة الدفعات أولاً.");
	}
}